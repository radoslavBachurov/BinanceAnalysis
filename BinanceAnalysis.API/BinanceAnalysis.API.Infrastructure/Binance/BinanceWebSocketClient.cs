
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
    using System.Collections.Concurrent;

    public class BinanceWebSocketClient(
        ILogger<BinanceWebSocketClient> logger) : IBinanceWebSocketClient
    {
        private const int BufferSize = 8192;
        private const int MaxMessageSize = 1024 * 1024;

        private static readonly TimeSpan SubscriptionAcknowledgementTimeout =
        TimeSpan.FromSeconds(10);

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

            var pendingAcknowledgements =
            new ConcurrentDictionary<int, TaskCompletionSource<bool>>();

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
                    pendingAcknowledgements,
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
                    pendingAcknowledgements,
                    token);

                    subscribedSymbols.ExceptWith(symbolsToUnsubscribe);
                }
            }

            using var linkedCts =
        CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken);

            // Start receiving messages first, so Binance acknowledgements
            // can be processed while subscription commands are pending.
            var receiveTask = ReceiveTradesAsync(
            socket,
            onTrade,
            logger,
            pendingAcknowledgements,
            linkedCts.Token);

            // Subscribe to the initial symbols after the receive loop starts.
            try
            {
                await UpdateSubscriptionsAsync(
                    initialSymbols,
                    linkedCts.Token);
            }
            catch
            {
                linkedCts.Cancel();

                try
                {
                    await receiveTask;
                }
                catch
                {
                    // Preserve the original subscription failure.
                }

                throw;
            }

            // Start monitoring runtime symbol changes.
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
    ConcurrentDictionary<int, TaskCompletionSource<bool>> pendingAcknowledgements,
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

            var acknowledgement = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!pendingAcknowledgements.TryAdd(requestId, acknowledgement))
            {
                throw new InvalidOperationException(
                    $"A subscription request with ID {requestId} is already pending.");
            }

            try
            {
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

                // Wait until the receive loop processes the matching response.
                await acknowledgement.Task.WaitAsync(
                    SubscriptionAcknowledgementTimeout,
                    cancellationToken);

                logger.LogInformation(
                    "Binance confirmed {Method} request for {SymbolCount} symbols. RequestId: {RequestId}",
                    method,
                    symbols.Length,
                    requestId);
            }
            catch (TimeoutException exception)
            {
                logger.LogError(
                    exception,
                    "Timed out waiting for Binance {Method} acknowledgement. RequestId: {RequestId}",
                    method,
                    requestId);

                throw;
            }
            finally
            {
                pendingAcknowledgements.TryRemove(requestId, out _);
            }
        }


        private static async Task ReceiveTradesAsync(
            ClientWebSocket socket,
            Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
            ILogger<BinanceWebSocketClient> logger,
            ConcurrentDictionary<int, TaskCompletionSource<bool>> pendingAcknowledgements,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];

            try
            {
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

                    if (message is null)
                    {
                        logger.LogWarning(
                            "Received an empty or invalid Binance message.");

                        continue;
                    }

                    // Handle subscription acknowledgements and errors.
                    if (message.Id.HasValue)
                    {
                        if (!pendingAcknowledgements.TryGetValue(
                                message.Id.Value,
                                out var pendingRequest))
                        {
                            logger.LogWarning(
                                "Received a Binance response for an unknown request ID: {RequestId}",
                                message.Id.Value);

                            continue;
                        }

                        if (message.Error is not null)
                        {
                            var error = new InvalidOperationException(
                                $"Binance rejected the subscription request. " +
                                $"Code: {message.Error.Code}. " +
                                $"Message: {message.Error.Message}");

                            logger.LogWarning(
                                "Binance rejected subscription request {RequestId}. Code: {Code}. Message: {Message}",
                                message.Id.Value,
                                message.Error.Code,
                                message.Error.Message);

                            pendingRequest.TrySetException(error);
                        }
                        else if (message.Result.ValueKind == JsonValueKind.Null)
                        {
                            logger.LogDebug(
                                "Received successful Binance subscription acknowledgement. RequestId: {RequestId}",
                                message.Id.Value);

                            pendingRequest.TrySetResult(true);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Received an unexpected Binance subscription response. RequestId: {RequestId}",
                                message.Id.Value);

                            pendingRequest.TrySetException(
                                new InvalidOperationException(
                                    $"Unexpected Binance response for request {message.Id.Value}."));
                        }

                        continue;
                    }

                    // Process normal trade events.
                    if (message.Data is not { } trade)
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
            finally
            {
                // Release any subscription requests still waiting for acknowledgements.
                foreach (var pendingRequest in pendingAcknowledgements.Values)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        pendingRequest.TrySetCanceled(cancellationToken);
                    }
                    else
                    {
                        pendingRequest.TrySetException(
                            new WebSocketException(
                                "Binance WebSocket connection ended before the subscription acknowledgement was received."));
                    }
                }
            }
        }
    }
}
