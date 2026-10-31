namespace BinanceAnalysis.Services.Data.Trading
{
    public interface IPriceAnalysisService
    {
        Task<decimal?> Get24HourAveragePriceAsync(
            string symbol,
            CancellationToken cancellationToken = default);

        Task<decimal?> GetSimpleMovingAverageAsync(
            string symbol,
            int numberOfDataPoints,
            TimeSpan period,
            DateTime? startDateTime = null,
            CancellationToken cancellationToken = default);
    }
}