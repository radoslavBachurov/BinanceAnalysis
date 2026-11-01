namespace BinanceAnalysis.API.Infrastructure.Binance
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;

    public interface IBinanceWebSocketClient
    {
        Task ListenToTradesAsync(
            string[] initialSymbols,
            Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
            Func<
                Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                CancellationToken,
                Task> runSubscriptionManager,
            CancellationToken cancellationToken);
    }
}