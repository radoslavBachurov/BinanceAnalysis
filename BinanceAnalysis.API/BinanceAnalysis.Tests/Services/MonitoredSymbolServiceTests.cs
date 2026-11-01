
namespace BinanceAnalysis.Tests.Services
{
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Symbols;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class MonitoredSymbolServiceTests
    {
        private static ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllConfiguredSymbols()
        {
            // Arrange
            await using var context = CreateDbContext();

            context.MonitoredSymbols.AddRange(
                new MonitoredSymbol { Symbol = "BTCUSDT" },
                new MonitoredSymbol { Symbol = "ADAUSDT" });

            await context.SaveChangesAsync();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(
                new[] { "ADAUSDT", "BTCUSDT" },
                result.Select(x => x.Symbol).ToArray());
        }

        [Fact]
        public async Task AddAsync_AddsNewActiveSymbol()
        {
            // Arrange
            await using var context = CreateDbContext();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.AddAsync("SOLUSDT");

            // Assert
            Assert.True(result);

            var savedSymbol = await context.MonitoredSymbols
                .SingleAsync(x => x.Symbol == "SOLUSDT");

            Assert.True(savedSymbol.IsActive);
            Assert.NotEqual(default, savedSymbol.CreatedAtUtc);
        }

        [Fact]
        public async Task AddAsync_ReturnsFalse_WhenSymbolAlreadyExists()
        {
            // Arrange
            await using var context = CreateDbContext();

            context.MonitoredSymbols.Add(
                new MonitoredSymbol { Symbol = "BTCUSDT" });

            await context.SaveChangesAsync();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.AddAsync("BTCUSDT");

            // Assert
            Assert.False(result);

            Assert.Equal(
                1,
                await context.MonitoredSymbols.CountAsync());
        }

        [Fact]
        public async Task SetActiveStatusAsync_DeactivatesExistingSymbol()
        {
            // Arrange
            await using var context = CreateDbContext();

            context.MonitoredSymbols.Add(
                new MonitoredSymbol
                {
                    Symbol = "SOLUSDT",
                    IsActive = true
                });

            await context.SaveChangesAsync();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.SetActiveStatusAsync(
                "SOLUSDT",
                false);

            // Assert
            Assert.True(result);

            var symbol = await context.MonitoredSymbols
                .SingleAsync(x => x.Symbol == "SOLUSDT");

            Assert.False(symbol.IsActive);
        }

        [Fact]
        public async Task SetActiveStatusAsync_ActivatesExistingSymbol()
        {
            // Arrange
            await using var context = CreateDbContext();

            context.MonitoredSymbols.Add(
                new MonitoredSymbol
                {
                    Symbol = "SOLUSDT",
                    IsActive = false
                });

            await context.SaveChangesAsync();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.SetActiveStatusAsync(
                "SOLUSDT",
                true);

            // Assert
            Assert.True(result);

            var symbol = await context.MonitoredSymbols
                .SingleAsync(x => x.Symbol == "SOLUSDT");

            Assert.True(symbol.IsActive);
        }

        [Fact]
        public async Task SetActiveStatusAsync_ReturnsFalse_WhenSymbolDoesNotExist()
        {
            // Arrange
            await using var context = CreateDbContext();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.SetActiveStatusAsync(
                "UNKNOWNUSDT",
                true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_RemovesExistingSymbol()
        {
            // Arrange
            await using var context = CreateDbContext();

            context.MonitoredSymbols.Add(
                new MonitoredSymbol { Symbol = "SOLUSDT" });

            await context.SaveChangesAsync();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.DeleteAsync("SOLUSDT");

            // Assert
            Assert.True(result);

            Assert.False(
                await context.MonitoredSymbols
                    .AnyAsync(x => x.Symbol == "SOLUSDT"));
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_WhenSymbolDoesNotExist()
        {
            // Arrange
            await using var context = CreateDbContext();

            var service = new MonitoredSymbolService(
                context,
                TimeProvider.System);

            // Act
            var result = await service.DeleteAsync("UNKNOWNUSDT");

            // Assert
            Assert.False(result);
        }
    }
}
