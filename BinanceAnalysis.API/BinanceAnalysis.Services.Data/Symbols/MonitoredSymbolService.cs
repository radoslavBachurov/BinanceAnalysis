namespace BinanceAnalysis.Services.Data.Symbols
{
    using BinanceAnalysis.API.Infrastructure.Binance.Validation;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;

    public class MonitoredSymbolService : IMonitoredSymbolService
    {
        private readonly ApplicationDbContext dbContext;
        private readonly TimeProvider timeProvider;
        private readonly IBinanceSymbolValidationClient binanceSymbolValidationClient;

        public MonitoredSymbolService(
            ApplicationDbContext dbContext,
            TimeProvider timeProvider,
            IBinanceSymbolValidationClient binanceSymbolValidationClient)
        {
            this.dbContext = dbContext;
            this.timeProvider = timeProvider;
            this.binanceSymbolValidationClient = binanceSymbolValidationClient;
        }

        public async Task<IReadOnlyCollection<MonitoredSymbol>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await dbContext.MonitoredSymbols
                .AsNoTracking()
                .OrderBy(x => x.Symbol)
                .ToListAsync(cancellationToken);
        }

        public async Task<AddMonitoredSymbolResult> AddAsync(
        string symbol,
        CancellationToken cancellationToken = default)
        {
            symbol = symbol.Trim().ToUpperInvariant();

            var alreadyExists = await dbContext.MonitoredSymbols
                .AnyAsync(x => x.Symbol == symbol, cancellationToken);

            if (alreadyExists)
            {
                return AddMonitoredSymbolResult.AlreadyExists;
            }

            var isValid = await binanceSymbolValidationClient
                .IsValidTradingSymbolAsync(symbol, cancellationToken);

            if (!isValid)
            {
                return AddMonitoredSymbolResult.InvalidBinanceSymbol;
            }

            var monitoredSymbol = new MonitoredSymbol
            {
                Symbol = symbol,
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };

            dbContext.MonitoredSymbols.Add(monitoredSymbol);

            await dbContext.SaveChangesAsync(cancellationToken);

            return AddMonitoredSymbolResult.Added;
        }

        public async Task<bool> SetActiveStatusAsync(
            string symbol,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            var monitoredSymbol = await dbContext.MonitoredSymbols
                .FirstOrDefaultAsync(
                    x => x.Symbol == symbol,
                    cancellationToken);

            if (monitoredSymbol is null)
            {
                return false;
            }

            monitoredSymbol.IsActive = isActive;

            await dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> DeleteAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var monitoredSymbol = await dbContext.MonitoredSymbols
                .FirstOrDefaultAsync(
                    x => x.Symbol == symbol,
                    cancellationToken);

            if (monitoredSymbol is null)
            {
                return false;
            }

            dbContext.MonitoredSymbols.Remove(monitoredSymbol);

            await dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}