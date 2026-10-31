
namespace BinanceAnalysis.API.Infrastructure.Binance
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;

    public class BinanceWebSocketClient
    {
        private const int BufferSize = 8192;
        private const int MaxMessageSize = 1024 * 1024;

        private const string BaseUrl =
            "wss://stream.binance.com:9443/stream?streams=";

        public async Task ListenToTradesAsync(
            string[] symbols,
            Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(symbols);
            ArgumentNullException.ThrowIfNull(onTrade);

            if (symbols.Length == 0)
            {
                throw new ArgumentException(
                    "At least one symbol must be provided.",
                    nameof(symbols));
            }

            var streams = string.Join(
                "/",
                symbols.Select(symbol => $"{symbol.ToLowerInvariant()}@trade"));

            var uri = new Uri($"{BaseUrl}{streams}");

            using var socket = new ClientWebSocket();

            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            await socket.ConnectAsync(uri, cancellationToken);

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
                        if (socket.State == WebSocketState.CloseReceived)
                        {
                            await socket.CloseOutputAsync(
                                socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                                socket.CloseStatusDescription,
                                CancellationToken.None);
                        }

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

                var message = JsonSerializer.Deserialize<BinanceCombinedStreamMessage>(
                    messageStream.ToArray());

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
