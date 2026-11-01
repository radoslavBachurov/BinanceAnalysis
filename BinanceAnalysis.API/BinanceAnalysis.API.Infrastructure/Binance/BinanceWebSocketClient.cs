namespace BinanceAnalysis.API.Infrastructure.Binance
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;

    public class BinanceWebSocketClient : IBinanceWebSocketClient
    {
        private const int BufferSize = 8192;
        private const int MaxMessageSize = 1024 * 1024;

        private static readonly Uri BaseUri =
            new("wss://stream.binance.com:9443/stream");

        public async Task ListenToTradesAsync(
            string[] initialSymbols,
            Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
            Func<
                Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                CancellationToken,
                Task> runSubscriptionManager,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(initialSymbols);
            ArgumentNullException.ThrowIfNull(onTrade);
            ArgumentNullException.ThrowIfNull(runSubscriptionManager);

            using var socket = new ClientWebSocket();
            using var sendLock = new SemaphoreSlim(1, 1);

            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            await socket.ConnectAsync(BaseUri, cancellationToken);

            var subscribedSymbols = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var requestId = 0;

            async Task UpdateSubscriptionsAsync(
                IReadOnlyCollection<string> requestedSymbols,
                CancellationToken token)
            {
                var desiredSymbols = requestedSymbols
                    .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                    .Select(symbol => symbol.Trim().ToUpperInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var symbolsToSubscribe = desiredSymbols
                    .Except(subscribedSymbols)
                    .ToArray();

                var symbolsToUnsubscribe = subscribedSymbols
                    .Except(desiredSymbols)
                    .ToArray();

                if (symbolsToSubscribe.Length > 0)
                {
                    await SendSubscriptionCommandAsync(
                        socket,
                        sendLock,
                        "SUBSCRIBE",
                        symbolsToSubscribe,
                        ++requestId,
                        token);

                    subscribedSymbols.UnionWith(symbolsToSubscribe);
                }

                if (symbolsToUnsubscribe.Length > 0)
                {
                    await SendSubscriptionCommandAsync(
                        socket,
                        sendLock,
                        "UNSUBSCRIBE",
                        symbolsToUnsubscribe,
                        ++requestId,
                        token);

                    subscribedSymbols.ExceptWith(symbolsToUnsubscribe);
                }
            }

            // Subscribe to the initial symbols.
            await UpdateSubscriptionsAsync(
                initialSymbols,
                cancellationToken);

            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            var receiveTask = ReceiveTradesAsync(
                socket,
                onTrade,
                linkedCts.Token);

            var subscriptionTask = runSubscriptionManager(
                UpdateSubscriptionsAsync,
                linkedCts.Token);

            try
            {
                await Task.WhenAny(receiveTask, subscriptionTask);
                linkedCts.Cancel();

                await Task.WhenAll(receiveTask, subscriptionTask);
            }
            catch (OperationCanceledException)
                when (linkedCts.IsCancellationRequested)
            {
                // Expected when the connection is closed or cancelled.
            }
        }

        private static async Task SendSubscriptionCommandAsync(
            ClientWebSocket socket,
            SemaphoreSlim sendLock,
            string method,
            string[] symbols,
            int requestId,
            CancellationToken cancellationToken)
        {
            var streams = symbols
                .Select(symbol => $"{symbol.ToLowerInvariant()}@trade")
                .ToArray();

            var command = JsonSerializer.SerializeToUtf8Bytes(new
            {
                method,
                @params = streams,
                id = requestId
            });

            await sendLock.WaitAsync(cancellationToken);

            try
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(command),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private static async Task ReceiveTradesAsync(
            ClientWebSocket socket,
            Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];

            while (!cancellationToken.IsCancellationRequested &&
                   socket.State == WebSocketState.Open)
            {
                using var messageStream = new MemoryStream();

                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        throw new InvalidDataException(
                            "Unexpected WebSocket message type.");
                    }

                    if (messageStream.Length + result.Count > MaxMessageSize)
                    {
                        throw new InvalidDataException(
                            "WebSocket message exceeded the maximum allowed size.");
                    }

                    await messageStream.WriteAsync(
                        buffer.AsMemory(0, result.Count),
                        cancellationToken);

                } while (!result.EndOfMessage);

                var message =
                    JsonSerializer.Deserialize<BinanceCombinedStreamMessage>(
                        messageStream.ToArray());

                // Subscription acknowledgements also arrive over this socket.
                // They don't contain a trade event, so we ignore them.
                if (message?.Data is not { } trade)
                {
                    continue;
                }

                if (trade.EventType != "trade")
                {
                    continue;
                }

                await onTrade(trade, cancellationToken);
            }
        }
    }
}
