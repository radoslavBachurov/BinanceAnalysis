namespace BinanceAnalysis.Services.Data.Symbols
{
    using BinanceAnalysis.Data.Models;

    public interface IMonitoredSymbolService
    {
        Task<IReadOnlyCollection<MonitoredSymbol>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<bool> AddAsync(
            string symbol,
            CancellationToken cancellationToken = default);

        Task<bool> SetActiveStatusAsync(
            string symbol,
            bool isActive,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            string symbol,
            CancellationToken cancellationToken = default);
    }
}