namespace BinanceAnalysis.Services.Data.Symbols
{
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;

    public class MonitoredSymbolService(
        ApplicationDbContext dbContext,
        TimeProvider timeProvider) : IMonitoredSymbolService
    {
        public async Task<IReadOnlyCollection<MonitoredSymbol>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await dbContext.MonitoredSymbols
                .AsNoTracking()
                .OrderBy(x => x.Symbol)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> AddAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var existingSymbol = await dbContext.MonitoredSymbols
                .AnyAsync(x => x.Symbol == symbol, cancellationToken);

            if (existingSymbol)
            {
                return false;
            }

            var monitoredSymbol = new MonitoredSymbol
            {
                Symbol = symbol,
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };

            await dbContext.MonitoredSymbols.AddAsync(
                monitoredSymbol,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return true;
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