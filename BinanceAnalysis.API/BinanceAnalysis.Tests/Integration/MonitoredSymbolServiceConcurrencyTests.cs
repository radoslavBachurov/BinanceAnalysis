namespace BinanceAnalysis.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance.Validation;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Symbols;
    using Microsoft.EntityFrameworkCore;
    using Moq;

    public class MonitoredSymbolServiceConcurrencyTests
        : IClassFixture<SqlServerIntegrationTestFixture>
    {
        private readonly SqlServerIntegrationTestFixture fixture;

        public MonitoredSymbolServiceConcurrencyTests(
            SqlServerIntegrationTestFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task SetActiveStatusAsync_DeactivatesExistingSymbol()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol, true);

            await using (var context = this.fixture.CreateDbContext())
            {
                var service = CreateService(context);

                var result = await service.SetActiveStatusAsync(symbol, false);

                Assert.True(result);
            }

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var entity = await verificationContext.MonitoredSymbols
                .SingleAsync(x => x.Symbol == symbol);

            Assert.False(entity.IsActive);
        }

        [Fact]
        public async Task SetActiveStatusAsync_ActivatesExistingSymbol()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol, false);

            await using (var context = this.fixture.CreateDbContext())
            {
                var service = CreateService(context);

                var result = await service.SetActiveStatusAsync(symbol, true);

                Assert.True(result);
            }

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var entity = await verificationContext.MonitoredSymbols
                .SingleAsync(x => x.Symbol == symbol);

            Assert.True(entity.IsActive);
        }

        [Fact]
        public async Task SetActiveStatusAsync_ReturnsFalse_WhenSymbolDoesNotExist()
        {
            var symbol = CreateSymbol();

            await using var context = this.fixture.CreateDbContext();

            var service = CreateService(context);

            var result = await service.SetActiveStatusAsync(symbol, true);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_RemovesExistingSymbol()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol);

            await using (var context = this.fixture.CreateDbContext())
            {
                var service = CreateService(context);

                var result = await service.DeleteAsync(symbol);

                Assert.True(result);
            }

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var exists = await verificationContext.MonitoredSymbols
                .AnyAsync(x => x.Symbol == symbol);

            Assert.False(exists);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_WhenSymbolDoesNotExist()
        {
            var symbol = CreateSymbol();

            await using var context = this.fixture.CreateDbContext();

            var service = CreateService(context);

            var result = await service.DeleteAsync(symbol);

            Assert.False(result);
        }

        [Fact]
        public async Task SetActiveStatusAsync_ConcurrentActivateAndDeactivate_BothComplete()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol, false);

            var activateTask = SetActiveStatusAsync(symbol, true);
            var deactivateTask = SetActiveStatusAsync(symbol, false);

            var results = await Task.WhenAll(
                activateTask,
                deactivateTask);

            Assert.All(results, result => Assert.True(result));

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var entity = await verificationContext.MonitoredSymbols
                .SingleAsync(x => x.Symbol == symbol);

            // The final value depends on which SQL update executes last.
            Assert.Contains(entity.IsActive, new[] { true, false });
        }

        [Fact]
        public async Task DeleteAsync_ConcurrentDeleteAndDeactivate_DeleteSucceeds()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol, true);

            var deleteTask = DeleteAsync(symbol);
            var deactivateTask = SetActiveStatusAsync(symbol, false);

            var results = await Task.WhenAll(
                deleteTask,
                deactivateTask);

            Assert.True(results[0]);
            Assert.Contains(results[1], new[] { true, false });

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var exists = await verificationContext.MonitoredSymbols
                .AnyAsync(x => x.Symbol == symbol);

            Assert.False(exists);
        }

        [Fact]
        public async Task DeleteAsync_ConcurrentDeletes_OnlyOneSucceeds()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol);

            var results = await Task.WhenAll(
                DeleteAsync(symbol),
                DeleteAsync(symbol));

            Assert.Equal(1, results.Count(result => result));
        }

        [Fact]
        public async Task SetActiveStatusAsync_ConcurrentActivations_BothSucceed()
        {
            var symbol = CreateSymbol();

            await SeedSymbolAsync(symbol, false);

            var results = await Task.WhenAll(
                SetActiveStatusAsync(symbol, true),
                SetActiveStatusAsync(symbol, true));

            Assert.All(results, result => Assert.True(result));

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var entity = await verificationContext.MonitoredSymbols
                .SingleAsync(x => x.Symbol == symbol);

            Assert.True(entity.IsActive);
        }

        [Fact]
        public async Task AddAsync_ConcurrentAdds_OnlyOneSucceeds()
        {
            var symbol = CreateSymbol();

            var interceptor =
                new ConcurrentMonitoredSymbolCommandInterceptor();

            await using var context1 =
                this.fixture.CreateDbContext(interceptor);

            await using var context2 =
                this.fixture.CreateDbContext(interceptor);

            var service1 = CreateService(context1);
            var service2 = CreateService(context2);

            var results = await Task.WhenAll(
                service1.AddAsync(symbol),
                service2.AddAsync(symbol));

            Assert.Equal(
                1,
                results.Count(
                    result => result == AddMonitoredSymbolResult.Added));

            Assert.Equal(
                1,
                results.Count(
                    result => result == AddMonitoredSymbolResult.AlreadyExists));

            await using var verificationContext =
                this.fixture.CreateDbContext();

            var count = await verificationContext.MonitoredSymbols
                .CountAsync(x => x.Symbol == symbol);

            Assert.Equal(1, count);
        }

        private async Task SeedSymbolAsync(
            string symbol,
            bool isActive = true)
        {
            await using var context = this.fixture.CreateDbContext();

            context.MonitoredSymbols.Add(new MonitoredSymbol
            {
                Symbol = symbol,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        private async Task<bool> SetActiveStatusAsync(
            string symbol,
            bool isActive)
        {
            await using var context = this.fixture.CreateDbContext();

            var service = CreateService(context);

            return await service.SetActiveStatusAsync(symbol, isActive);
        }

        private async Task<bool> DeleteAsync(string symbol)
        {
            await using var context = this.fixture.CreateDbContext();

            var service = CreateService(context);

            return await service.DeleteAsync(symbol);
        }

        private static MonitoredSymbolService CreateService(
            ApplicationDbContext context)
        {
            var validationClient =
                new Mock<IBinanceSymbolValidationClient>();

            validationClient
                .Setup(x => x.IsValidTradingSymbolAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            return new MonitoredSymbolService(
                context,
                TimeProvider.System,
                validationClient.Object);
        }

        private static string CreateSymbol()
        {
            return $"T{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        }
    }
}