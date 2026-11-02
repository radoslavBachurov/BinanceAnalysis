namespace BinanceAnalysis.Services.Data.Symbols
{
    using BinanceAnalysis.API.Infrastructure.Binance.Validation;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Data.SqlClient;

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

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return AddMonitoredSymbolResult.Added;
            }
            catch (DbUpdateException exception)
            {
                if (exception.GetBaseException() is SqlException sqlException &&
                    sqlException.Number is 2601 or 2627)
                {
                    dbContext.Entry(monitoredSymbol).State = EntityState.Detached;

                    return AddMonitoredSymbolResult.AlreadyExists;
                }

                throw;
            }
        }


        public async Task<bool> SetActiveStatusAsync(
            string symbol,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            symbol = symbol.Trim().ToUpperInvariant();

            var affectedRows = await dbContext.MonitoredSymbols
                .Where(x => x.Symbol == symbol)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.IsActive,
                        isActive),
                    cancellationToken);

            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            symbol = symbol.Trim().ToUpperInvariant();

            var affectedRows = await dbContext.MonitoredSymbols
                .Where(x => x.Symbol == symbol)
                .ExecuteDeleteAsync(cancellationToken);

            return affectedRows > 0;
        }

    }
}