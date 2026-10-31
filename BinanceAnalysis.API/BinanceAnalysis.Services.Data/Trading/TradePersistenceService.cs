
namespace BinanceAnalysis.Services.Data.Trading
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    public class TradePersistenceService
    {
        private readonly ApplicationDbContext context;
        private readonly ILogger<TradePersistenceService> logger;

        public TradePersistenceService(
            ApplicationDbContext context,
            ILogger<TradePersistenceService> logger)
        {
            this.context = context;
            this.logger = logger;
        }

        public async Task<int> PersistBatchAsync(
            IReadOnlyCollection<BinanceTradeEvent> trades,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(trades);

            if (trades.Count == 0)
            {
                return 0;
            }

            var marketPrices = new List<MarketPrice>();

            foreach (var trade in trades)
            {
                if (trade.EventType != "trade" ||
                    string.IsNullOrWhiteSpace(trade.Symbol) ||
                    trade.TradeId < 0 ||
                    !decimal.TryParse(
                        trade.Price,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var price) ||
                    price <= 0)
                {
                    this.logger.LogWarning(
                        "Ignoring invalid Binance trade message for symbol {Symbol}.",
                        trade.Symbol);

                    continue;
                }

                DateTime observedAtUtc;

                try
                {
                    observedAtUtc = DateTimeOffset
                        .FromUnixTimeMilliseconds(trade.TradeTime)
                        .UtcDateTime;
                }
                catch (ArgumentOutOfRangeException)
                {
                    this.logger.LogWarning(
                        "Ignoring trade {TradeId} for {Symbol}: invalid trade timestamp.",
                        trade.TradeId,
                        trade.Symbol);

                    continue;
                }

                marketPrices.Add(new MarketPrice
                {
                    Symbol = trade.Symbol,
                    TradeId = trade.TradeId,
                    Price = price,
                    ObservedAtUtc = observedAtUtc,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            if (marketPrices.Count == 0)
            {
                return 0;
            }

            var uniqueTrades = marketPrices
                .GroupBy(x => new { x.Symbol, x.TradeId })
                .Select(group => group.First())
                .ToList();

            var newTrades = new List<MarketPrice>();

            foreach (var group in uniqueTrades.GroupBy(x => x.Symbol))
            {
                var tradeIds = group.Select(x => x.TradeId).ToList();

                var existingTradeIds = await this.context.MarketPrices
                    .Where(x =>
                        x.Symbol == group.Key &&
                        tradeIds.Contains(x.TradeId))
                    .Select(x => x.TradeId)
                    .ToListAsync(cancellationToken);

                var existingIds = existingTradeIds.ToHashSet();

                newTrades.AddRange(
                    group.Where(x => !existingIds.Contains(x.TradeId)));
            }

            if (newTrades.Count == 0)
            {
                return 0;
            }

            await this.context.MarketPrices.AddRangeAsync(
                newTrades,
                cancellationToken);

            var insertedCount = await this.context.SaveChangesAsync(
                cancellationToken);

            this.logger.LogInformation(
                "Persisted {TradeCount} market trades.",
                insertedCount);

            return insertedCount;
        }
    }
}
