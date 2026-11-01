
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
    using Microsoft.Data.SqlClient;

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

            const int chunkSize = 1000;

            foreach (var group in uniqueTrades.GroupBy(x => x.Symbol))
            {
                var tradeIds = group
                    .Select(x => x.TradeId)
                    .ToList();

                var existingIds = new HashSet<long>();

                foreach (var chunk in tradeIds.Chunk(chunkSize))
                {
                    var existingTradeIds = await this.context.MarketPrices
                        .Where(x =>
                            x.Symbol == group.Key &&
                            chunk.Contains(x.TradeId))
                        .Select(x => x.TradeId)
                        .ToListAsync(cancellationToken);

                    existingIds.UnionWith(existingTradeIds);
                }

                newTrades.AddRange(
                    group.Where(x => !existingIds.Contains(x.TradeId)));
            }

            if (newTrades.Count == 0)
            {
                return 0;
            }

            const int maxSaveAttempts = 3;

            var pendingTrades = newTrades;

            for (var attempt = 1; attempt <= maxSaveAttempts; attempt++)
            {
                await this.context.MarketPrices.AddRangeAsync(
                    pendingTrades,
                    cancellationToken);

                try
                {
                    var insertedCount = await this.context.SaveChangesAsync(
                        cancellationToken);

                    this.logger.LogInformation(
                        "Persisted {TradeCount} market trades.",
                        insertedCount);

                    return insertedCount;
                }
                catch (DbUpdateException exception)
                    when (IsDuplicateKeyException(exception))
                {
                    // Detach only the entities involved in this failed attempt.
                    foreach (var trade in pendingTrades)
                    {
                        var entry = this.context.Entry(trade);

                        if (entry.State == EntityState.Added)
                        {
                            entry.State = EntityState.Detached;
                        }
                    }

                    // Check which candidate trades were inserted by other writers.
                    var existingKeys = await this.FindExistingTradeKeysAsync(
                        pendingTrades,
                        chunkSize,
                        cancellationToken);

                    pendingTrades = pendingTrades
                        .Where(x => !existingKeys.Contains((x.Symbol, x.TradeId)))
                        .ToList();

                    // All candidates were inserted by another writer.
                    if (pendingTrades.Count == 0)
                    {
                        this.logger.LogInformation(
                            "All trades in the batch were already persisted by another writer.");

                        return 0;
                    }

                    // Do not retry indefinitely.
                    if (attempt == maxSaveAttempts)
                    {
                        this.logger.LogError(
                            exception,
                            "Could not persist {TradeCount} trades after {MaxAttempts} attempts.",
                            pendingTrades.Count,
                            maxSaveAttempts);

                        throw;
                    }

                    this.logger.LogWarning(
                        exception,
                        "Duplicate trade conflict. Retrying {TradeCount} remaining trades. Attempt {Attempt} of {MaxAttempts}.",
                        pendingTrades.Count,
                        attempt + 1,
                        maxSaveAttempts);
                }
            }

            return 0;
        }

        private static bool IsDuplicateKeyException(Exception exception)
        {
            for (var current = exception;
                 current != null;
                 current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    (sqlException.Number == 2601 ||
                     sqlException.Number == 2627))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<HashSet<(string Symbol, long TradeId)>>
    FindExistingTradeKeysAsync(
        IReadOnlyCollection<MarketPrice> candidates,
        int chunkSize,
        CancellationToken cancellationToken)
        {
            var existingKeys = new HashSet<(string Symbol, long TradeId)>();

            foreach (var group in candidates.GroupBy(x => x.Symbol))
            {
                var tradeIds = group
                    .Select(x => x.TradeId)
                    .Distinct()
                    .ToList();

                foreach (var chunk in tradeIds.Chunk(chunkSize))
                {
                    var existingTradeIds = await this.context.MarketPrices
                        .Where(x =>
                            x.Symbol == group.Key &&
                            chunk.Contains(x.TradeId))
                        .Select(x => x.TradeId)
                        .ToListAsync(cancellationToken);

                    foreach (var tradeId in existingTradeIds)
                    {
                        existingKeys.Add((group.Key, tradeId));
                    }
                }
            }

            return existingKeys;
        }
    }
}
