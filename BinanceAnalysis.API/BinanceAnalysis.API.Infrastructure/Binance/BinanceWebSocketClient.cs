
namespace BinanceAnalysis.API.Infrastructure.Binance
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Runtime.ExceptionServices;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;
    using Microsoft.Extensions.Logging;

    public class BinanceWebSocketClient(
        ILogger<BinanceWebSocketClient> logger) : IBinanceWebSocketClient
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

            logger.LogInformation(
                "Successfully connected to Binance WebSocket endpoint {Endpoint}.",
                BaseUri);

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
                        logger,
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
                        logger,
                        "UNSUBSCRIBE",
                        symbolsToUnsubscribe,
                        ++requestId,
                        token);

                    subscribedSymbols.ExceptWith(symbolsToUnsubscribe);
                }
            }

            await UpdateSubscriptionsAsync(
                initialSymbols,
                cancellationToken);

            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            var receiveTask = ReceiveTradesAsync(
                socket,
                onTrade,
                logger,
                linkedCts.Token);

            var subscriptionTask = runSubscriptionManager(
                UpdateSubscriptionsAsync,
                linkedCts.Token);

            var completedTask = await Task.WhenAny(
                receiveTask,
                subscriptionTask);

            linkedCts.Cancel();

            Exception? failure = null;

            try
            {
                await receiveTask;
            }
            catch (OperationCanceledException)
                when (linkedCts.IsCancellationRequested)
            {
                // Expected when the connection is closed or cancelled.
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            try
            {
                await subscriptionTask;
            }
            catch (OperationCanceledException)
                when (linkedCts.IsCancellationRequested)
            {
                // Expected when the connection is closed or cancelled.
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            if (completedTask == subscriptionTask &&
                !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "The Binance subscription manager stopped unexpectedly.");
            }
        }

        private static async Task SendSubscriptionCommandAsync(
            ClientWebSocket socket,
            SemaphoreSlim sendLock,
            ILogger<BinanceWebSocketClient> logger,
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

                logger.LogInformation(
                    "Sent Binance {Method} request for {SymbolCount} symbols: {Symbols}. RequestId: {RequestId}",
                    method,
                    symbols.Length,
                    string.Join(", ", symbols),
                    requestId);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private static async Task ReceiveTradesAsync(
            ClientWebSocket socket,
            Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
            ILogger<BinanceWebSocketClient> logger,
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
                        logger.LogWarning(
                            "Binance WebSocket sent a close frame. Status: {CloseStatus}. Description: {CloseDescription}",
                            result.CloseStatus,
                            result.CloseStatusDescription);

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

                var payload = messageStream.ToArray();

                var message =
                    JsonSerializer.Deserialize<BinanceCombinedStreamMessage>(
                        payload);

                // Subscription acknowledgements don't contain trade data.
                if (message?.Data is not { } trade)
                {
                    LogSubscriptionResponse(payload, logger);
                    continue;
                }

                if (trade.EventType != "trade")
                {
                    continue;
                }

                await onTrade(trade, cancellationToken);
            }
        }

        private static void LogSubscriptionResponse(
            byte[] payload,
            ILogger<BinanceWebSocketClient> logger)
        {
            using var document = JsonDocument.Parse(payload);

            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var codeElement)
                    ? codeElement.ToString()
                    : "Unknown";

                var message = error.TryGetProperty("msg", out var messageElement)
                    ? messageElement.GetString()
                    : "Unknown Binance error";

                var requestId = root.TryGetProperty("id", out var idElement)
                    ? idElement.ToString()
                    : "Unknown";

                logger.LogWarning(
                    "Binance rejected a subscription request. RequestId: {RequestId}. Code: {Code}. Message: {Message}",
                    requestId,
                    code,
                    message);

                return;
            }

            if (root.TryGetProperty("result", out _) &&
                root.TryGetProperty("id", out var responseId))
            {
                logger.LogDebug(
                    "Received Binance subscription acknowledgement. RequestId: {RequestId}",
                    responseId.ToString());
            }
        }
    }
}
