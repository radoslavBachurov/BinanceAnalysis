namespace BinanceAnalysis.Console.Services
{
    using BinanceAnalysis.Console.Models;
    public interface IPriceAnalysisApiClient
    {
        Task<decimal?> Get24HourAveragePriceAsync(
            string symbol,
            CancellationToken cancellationToken = default);

        Task<decimal?> GetSimpleMovingAverageAsync(
            string symbol,
            int numberOfDataPoints,
            string timePeriod,
            DateTime? startDateTime = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MonitoredSymbolDto>> GetMonitoredSymbolsAsync(
            CancellationToken cancellationToken = default);

        Task<bool> AddMonitoredSymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default);

        Task<bool> SetMonitoredSymbolStatusAsync(
            string symbol,
            bool isActive,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteMonitoredSymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default);

        Task<bool> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
    }
}