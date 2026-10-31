namespace BinanceAnalysis.Services.Data.Trading
{
    using BinanceAnalysis.Data;
    using Microsoft.EntityFrameworkCore;

    public class PriceAnalysisService : IPriceAnalysisService
    {
        private readonly ApplicationDbContext dbContext;

        public PriceAnalysisService(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<decimal?> Get24HourAveragePriceAsync(
    string symbol,
    CancellationToken cancellationToken = default)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddHours(-24);

            var average = await this.dbContext.MarketPrices
                .AsNoTracking()
                .Where(x =>
                    x.Symbol == symbol &&
                    x.ObservedAtUtc >= startDate &&
                    x.ObservedAtUtc <= endDate)
                .Select(x => (decimal?)x.Price)
                .AverageAsync(cancellationToken);

            return average;
        }

        public async Task<decimal?> GetSimpleMovingAverageAsync(
            string symbol,
            int numberOfDataPoints,
            TimeSpan period,
            DateTime? startDateTime = null,
            CancellationToken cancellationToken = default)
        {
            if (numberOfDataPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numberOfDataPoints));
            }

            if (period <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }

            var startDate = startDateTime?.ToUniversalTime()
                ?? DateTime.UtcNow.Subtract(period);

            var endDate = startDateTime.HasValue
                ? startDate.Add(period)
                : DateTime.UtcNow;

            var prices = await this.dbContext.MarketPrices
                .AsNoTracking()
                .Where(x =>
                    x.Symbol == symbol &&
                    x.ObservedAtUtc >= startDate &&
                    x.ObservedAtUtc <= endDate)
                .OrderByDescending(x => x.ObservedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Price)
                .Take(numberOfDataPoints)
                .ToListAsync(cancellationToken);

            if (prices.Count < numberOfDataPoints)
            {
                return null;
            }

            return prices.Average();
        }
    }
}