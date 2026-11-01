namespace BinanceAnalysis.Console.Services
{
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
    }
}