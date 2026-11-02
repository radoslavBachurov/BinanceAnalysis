
namespace BinanceAnalysis.Tests.Services
{
    using BinanceAnalysis.API.Infrastructure.Binance.Validation;
    using BinanceAnalysis.API.ViewModels.Symbols;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Symbols;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Moq;
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

        private static MonitoredSymbolService CreateService(
            ApplicationDbContext context,
            Mock<IBinanceSymbolValidationClient> binanceClientMock)
        {
            return new MonitoredSymbolService(
                context,
                TimeProvider.System,
                binanceClientMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllConfiguredSymbols()
        {
            // Arrange
            await using var context = CreateDbContext();
            var binanceClientMock = new Mock<IBinanceSymbolValidationClient>();

            context.MonitoredSymbols.AddRange(
                new MonitoredSymbol { Symbol = "BTCUSDT" },
                new MonitoredSymbol { Symbol = "ADAUSDT" });

            await context.SaveChangesAsync();

            var service = CreateService(context, binanceClientMock);

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
            var binanceClientMock = new Mock<IBinanceSymbolValidationClient>();

            binanceClientMock
                .Setup(x => x.IsValidTradingSymbolAsync(
                    "SOLUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = CreateService(context, binanceClientMock);

            // Act
            var result = await service.AddAsync("SOLUSDT");

            // Assert
            Assert.Equal(AddMonitoredSymbolResult.Added, result);

            var savedSymbol = await context.MonitoredSymbols
                .SingleAsync(x => x.Symbol == "SOLUSDT");

            Assert.True(savedSymbol.IsActive);
            Assert.NotEqual(default, savedSymbol.CreatedAtUtc);

            binanceClientMock.Verify(
                x => x.IsValidTradingSymbolAsync(
                    "SOLUSDT",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AddAsync_ReturnsAlreadyExists_WhenSymbolAlreadyExists()
        {
            // Arrange
            await using var context = CreateDbContext();
            var binanceClientMock = new Mock<IBinanceSymbolValidationClient>();

            context.MonitoredSymbols.Add(
                new MonitoredSymbol { Symbol = "BTCUSDT" });

            await context.SaveChangesAsync();

            var service = CreateService(context, binanceClientMock);

            // Act
            var result = await service.AddAsync("BTCUSDT");

            // Assert
            Assert.Equal(AddMonitoredSymbolResult.AlreadyExists, result);

            Assert.Equal(
                1,
                await context.MonitoredSymbols.CountAsync());

            binanceClientMock.Verify(
                x => x.IsValidTradingSymbolAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task AddAsync_ReturnsInvalidBinanceSymbol_WhenSymbolIsNotValid()
        {
            // Arrange
            await using var context = CreateDbContext();
            var binanceClientMock = new Mock<IBinanceSymbolValidationClient>();

            binanceClientMock
                .Setup(x => x.IsValidTradingSymbolAsync(
                    "INVALIDUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var service = CreateService(context, binanceClientMock);

            // Act
            var result = await service.AddAsync("INVALIDUSDT");

            // Assert
            Assert.Equal(
                AddMonitoredSymbolResult.InvalidBinanceSymbol,
                result);

            Assert.False(
                await context.MonitoredSymbols
                    .AnyAsync(x => x.Symbol == "INVALIDUSDT"));
        }
    }
}
