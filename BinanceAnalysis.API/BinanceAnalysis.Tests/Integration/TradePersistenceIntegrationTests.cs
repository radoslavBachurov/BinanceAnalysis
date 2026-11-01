namespace BinanceAnalysis.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.EntityFrameworkCore;

    public class TradePersistenceIntegrationTests
        : IClassFixture<SqlServerIntegrationTestFixture>
    {
        private readonly SqlServerIntegrationTestFixture fixture;

        public TradePersistenceIntegrationTests(
            SqlServerIntegrationTestFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task PersistBatchAsync_RepeatedBatch_DoesNotInsertDuplicates()
        {
            // Arrange
            var symbol = $"TEST{Guid.NewGuid():N}"[..10];

            var trades = new List<BinanceTradeEvent>
            {
                CreateTrade(symbol, 1001, "65000.25"),
                CreateTrade(symbol, 1002, "65001.50"),
                CreateTrade(symbol, 1003, "65002.75")
            };

            await using var context = this.fixture.CreateDbContext();

            var service = new TradePersistenceService(
                context,
                NullLogger<TradePersistenceService>.Instance);

            // Act
            var firstInsertCount = await service.PersistBatchAsync(trades);

            var secondInsertCount = await service.PersistBatchAsync(trades);

            // Assert
            Assert.Equal(3, firstInsertCount);
            Assert.Equal(0, secondInsertCount);

            var persistedCount = await context.MarketPrices
                .CountAsync(x => x.Symbol == symbol);

            Assert.Equal(3, persistedCount);
        }

        [Fact]
        public async Task PersistBatchAsync_OverlappingBatches_InsertsOnlyNewTrades()
        {
            // Arrange
            var symbol = $"TEST{Guid.NewGuid():N}"[..10];

            var firstBatch = new List<BinanceTradeEvent>
    {
        CreateTrade(symbol, 2001, "65000.25"),
        CreateTrade(symbol, 2002, "65001.50"),
        CreateTrade(symbol, 2003, "65002.75")
    };

            var secondBatch = new List<BinanceTradeEvent>
    {
        CreateTrade(symbol, 2002, "65001.50"),
        CreateTrade(symbol, 2003, "65002.75"),
        CreateTrade(symbol, 2004, "65003.00")
    };

            await using var context = this.fixture.CreateDbContext();

            var service = new TradePersistenceService(
                context,
                NullLogger<TradePersistenceService>.Instance);

            // Act
            var firstInsertCount =
                await service.PersistBatchAsync(firstBatch);

            var secondInsertCount =
                await service.PersistBatchAsync(secondBatch);

            // Assert
            Assert.Equal(3, firstInsertCount);
            Assert.Equal(1, secondInsertCount);

            var persistedTrades = await context.MarketPrices
                .Where(x => x.Symbol == symbol)
                .OrderBy(x => x.TradeId)
                .ToListAsync();

            Assert.Equal(4, persistedTrades.Count);

            Assert.Equal(
                new long[] { 2001, 2002, 2003, 2004 },
                persistedTrades.Select(x => x.TradeId).ToArray());
        }

        [Fact]
        public async Task PersistBatchAsync_ConcurrentDuplicate_DoesNotCreateDuplicates()
        {
            // Arrange
            var symbol = $"TEST{Guid.NewGuid():N}"[..10];

            var trade = CreateTrade(symbol, 3001, "65000.25");

            var interceptor = new ConcurrentTradeCommandInterceptor();

            await using var context1 =
                this.fixture.CreateDbContext(interceptor);

            await using var context2 =
                this.fixture.CreateDbContext(interceptor);

            var service1 = new TradePersistenceService(
                context1,
                NullLogger<TradePersistenceService>.Instance);

            var service2 = new TradePersistenceService(
                context2,
                NullLogger<TradePersistenceService>.Instance);

            // Act
            var results = await Task.WhenAll(
                service1.PersistBatchAsync(new[] { trade }),
                service2.PersistBatchAsync(new[] { trade }));

            // Assert
            Assert.Equal(1, results.Sum());

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var persistedCount = await verificationContext.MarketPrices
                .CountAsync(x =>
                    x.Symbol == symbol &&
                    x.TradeId == trade.TradeId);

            Assert.Equal(1, persistedCount);
        }

        [Fact]
        public async Task PersistBatchAsync_ConcurrentOverlappingBatches_PersistsAllUniqueTrades()
        {
            // Arrange
            var symbol = $"TEST{Guid.NewGuid():N}"[..10];

            var sharedTrade = CreateTrade(symbol, 4001, "65000.25");

            var batchA = new List<BinanceTradeEvent>
    {
        sharedTrade,
        CreateTrade(symbol, 4002, "65001.50")
    };

            var batchB = new List<BinanceTradeEvent>
    {
        CreateTrade(symbol, 4001, "65000.25"),
        CreateTrade(symbol, 4003, "65002.75")
    };

            var interceptor = new ConcurrentTradeCommandInterceptor();

            await using var context1 =
                this.fixture.CreateDbContext(interceptor);

            await using var context2 =
                this.fixture.CreateDbContext(interceptor);

            var service1 = new TradePersistenceService(
                context1,
                NullLogger<TradePersistenceService>.Instance);

            var service2 = new TradePersistenceService(
                context2,
                NullLogger<TradePersistenceService>.Instance);

            // Act
            var results = await Task.WhenAll(
                service1.PersistBatchAsync(batchA),
                service2.PersistBatchAsync(batchB));

            // Assert
            Assert.Equal(3, results.Sum());

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var persistedTrades = await verificationContext.MarketPrices
                .Where(x => x.Symbol == symbol)
                .OrderBy(x => x.TradeId)
                .ToListAsync();

            Assert.Equal(3, persistedTrades.Count);

            Assert.Equal(
                new long[] { 4001, 4002, 4003 },
                persistedTrades.Select(x => x.TradeId).ToArray());
        }

        private static BinanceTradeEvent CreateTrade(
            string symbol,
            long tradeId,
            string price)
        {
            return new BinanceTradeEvent
            {
                EventType = "trade",
                EventTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Symbol = symbol,
                TradeId = tradeId,
                Price = price,
                Quantity = "0.01",
                TradeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsBuyerMarketMaker = false
            };
        }
    }
}