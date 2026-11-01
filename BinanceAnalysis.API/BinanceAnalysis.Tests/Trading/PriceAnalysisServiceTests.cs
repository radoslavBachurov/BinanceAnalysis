namespace BinanceAnalysis.Tests.Trading
{
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class PriceAnalysisServiceTests
    {
        [Fact]
        public async Task Get24HourAveragePriceAsync_ReturnsAverageOfPricesWithinLast24Hours()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var dbContext = new ApplicationDbContext(options);

            var now = DateTime.UtcNow;

            dbContext.MarketPrices.AddRange(
                new MarketPrice
                {
                    Symbol = "BTCUSDT",
                    TradeId = 1,
                    Price = 100m,
                    ObservedAtUtc = now.AddHours(-2),
                    CreatedAtUtc = now
                },
                new MarketPrice
                {
                    Symbol = "BTCUSDT",
                    TradeId = 2,
                    Price = 200m,
                    ObservedAtUtc = now.AddHours(-1),
                    CreatedAtUtc = now
                },
                new MarketPrice
                {
                    Symbol = "BTCUSDT",
                    TradeId = 3,
                    Price = 500m,
                    ObservedAtUtc = now.AddHours(-25),
                    CreatedAtUtc = now
                });

            await dbContext.SaveChangesAsync();

            var service = new PriceAnalysisService(dbContext);

            // Act
            var result = await service.Get24HourAveragePriceAsync("BTCUSDT");

            // Assert
            Assert.Equal(150m, result);
        }

        [Fact]
        public async Task GetSimpleMovingAverageAsync_ReturnsAverage_WhenExactlyNTradesExist()
        {
            // Arrange
            await using var dbContext = CreateDbContext();

            var startDate = new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);

            dbContext.MarketPrices.AddRange(
                CreatePrice("BTCUSDT", 1, 100m, startDate.AddMinutes(1)),
                CreatePrice("BTCUSDT", 2, 200m, startDate.AddMinutes(2)),
                CreatePrice("BTCUSDT", 3, 300m, startDate.AddMinutes(3)));

            await dbContext.SaveChangesAsync();

            var service = new PriceAnalysisService(dbContext);

            // Act
            var result = await service.GetSimpleMovingAverageAsync(
                "BTCUSDT",
                3,
                TimeSpan.FromMinutes(5),
                startDate);

            // Assert
            Assert.Equal(200m, result);
        }

        [Fact]
        public async Task GetSimpleMovingAverageAsync_ReturnsNull_WhenNotEnoughTradesExist()
        {
            // Arrange
            await using var dbContext = CreateDbContext();

            var startDate = new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);

            dbContext.MarketPrices.AddRange(
                CreatePrice("BTCUSDT", 1, 100m, startDate.AddMinutes(1)),
                CreatePrice("BTCUSDT", 2, 200m, startDate.AddMinutes(2)));

            await dbContext.SaveChangesAsync();

            var service = new PriceAnalysisService(dbContext);

            // Act
            var result = await service.GetSimpleMovingAverageAsync(
                "BTCUSDT",
                3,
                TimeSpan.FromMinutes(5),
                startDate);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetSimpleMovingAverageAsync_ExcludesTradesOutsidePeriod()
        {
            // Arrange
            await using var dbContext = CreateDbContext();

            var startDate = new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);

            dbContext.MarketPrices.AddRange(
                CreatePrice("BTCUSDT", 1, 1000m, startDate.AddMinutes(-1)),
                CreatePrice("BTCUSDT", 2, 100m, startDate.AddMinutes(1)),
                CreatePrice("BTCUSDT", 3, 200m, startDate.AddMinutes(2)),
                CreatePrice("BTCUSDT", 4, 900m, startDate.AddMinutes(6)));

            await dbContext.SaveChangesAsync();

            var service = new PriceAnalysisService(dbContext);

            // Act
            var result = await service.GetSimpleMovingAverageAsync(
                "BTCUSDT",
                2,
                TimeSpan.FromMinutes(5),
                startDate);

            // Assert
            Assert.Equal(150m, result);
        }

        [Fact]
        public async Task GetSimpleMovingAverageAsync_UsesSuppliedStartDate()
        {
            // Arrange
            await using var dbContext = CreateDbContext();

            var startDate = new DateTime(2020, 1, 1, 10, 0, 0, DateTimeKind.Utc);

            dbContext.MarketPrices.AddRange(
                CreatePrice("BTCUSDT", 1, 100m, startDate.AddMinutes(1)),
                CreatePrice("BTCUSDT", 2, 200m, startDate.AddMinutes(2)),
                CreatePrice("BTCUSDT", 3, 900m, DateTime.UtcNow.AddMinutes(-2)),
                CreatePrice("BTCUSDT", 4, 1000m, DateTime.UtcNow.AddMinutes(-1)));

            await dbContext.SaveChangesAsync();

            var service = new PriceAnalysisService(dbContext);

            // Act
            var result = await service.GetSimpleMovingAverageAsync(
                "BTCUSDT",
                2,
                TimeSpan.FromMinutes(5),
                startDate);

            // Assert
            Assert.Equal(150m, result);
        }

        private static ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static MarketPrice CreatePrice(
            string symbol,
            long tradeId,
            decimal price,
            DateTime observedAtUtc)
        {
            return new MarketPrice
            {
                Symbol = symbol,
                TradeId = tradeId,
                Price = price,
                ObservedAtUtc = observedAtUtc,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
    }
}