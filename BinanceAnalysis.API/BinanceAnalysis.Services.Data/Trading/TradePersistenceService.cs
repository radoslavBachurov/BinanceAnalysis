
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

            var receivedCount = trades.Count;
            var invalidCount = 0;

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
                    invalidCount++;
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
                    invalidCount++;
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

            if (invalidCount > 0)
            {
                this.logger.LogWarning(
                    "Ignored {InvalidCount} invalid trades out of {ReceivedCount} received trades.",
                    invalidCount,
                    receivedCount);
            }

            if (marketPrices.Count == 0)
            {
                this.logger.LogInformation(
                    "Trade batch contained no valid trades. Received: {ReceivedCount}. Invalid: {InvalidCount}.",
                    receivedCount,
                    invalidCount);

                return 0;
            }

            var uniqueTrades = marketPrices
                .GroupBy(x => new { x.Symbol, x.TradeId })
                .Select(group => group.First())
                .ToList();

            var duplicatesInBatch = marketPrices.Count - uniqueTrades.Count;

            const int chunkSize = 1000;

            var newTrades = new List<MarketPrice>();

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

            var existingCount = uniqueTrades.Count - newTrades.Count;

            this.logger.LogDebug(
                "Prepared trade batch. Received: {ReceivedCount}, Invalid: {InvalidCount}, Duplicates within batch: {DuplicatesInBatch}, Already in database: {ExistingCount}, Candidates for insertion: {NewTradeCount}.",
                receivedCount,
                invalidCount,
                duplicatesInBatch,
                existingCount,
                newTrades.Count);

            if (newTrades.Count == 0)
            {
                this.logger.LogInformation(
                    "No new trades to persist. Received: {ReceivedCount}, Invalid: {InvalidCount}, Duplicates within batch: {DuplicatesInBatch}, Already in database: {ExistingCount}.",
                    receivedCount,
                    invalidCount,
                    duplicatesInBatch,
                    existingCount);

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
                        "Persisted {InsertedCount} market trades. Received: {ReceivedCount}, Invalid: {InvalidCount}, Duplicates within batch: {DuplicatesInBatch}, Already in database: {ExistingCount}.",
                        insertedCount,
                        receivedCount,
                        invalidCount,
                        duplicatesInBatch,
                        existingCount);

                    return insertedCount;
                }
                catch (DbUpdateException exception)
                    when (IsDuplicateKeyException(exception))
                {
                    foreach (var trade in pendingTrades)
                    {
                        var entry = this.context.Entry(trade);

                        if (entry.State == EntityState.Added)
                        {
                            entry.State = EntityState.Detached;
                        }
                    }

                    var existingKeys = await this.FindExistingTradeKeysAsync(
                        pendingTrades,
                        chunkSize,
                        cancellationToken);

                    pendingTrades = pendingTrades
                        .Where(x => !existingKeys.Contains((x.Symbol, x.TradeId)))
                        .ToList();

                    if (pendingTrades.Count == 0)
                    {
                        this.logger.LogInformation(
                            "All candidate trades were persisted by another writer. Duplicate conflicts resolved successfully.");

                        return 0;
                    }

                    if (attempt == maxSaveAttempts)
                    {
                        this.logger.LogError(
                            exception,
                            "Could not persist {TradeCount} remaining trades after {MaxAttempts} attempts.",
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
