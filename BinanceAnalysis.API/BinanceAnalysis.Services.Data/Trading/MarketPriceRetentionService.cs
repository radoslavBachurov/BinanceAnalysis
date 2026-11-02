
namespace BinanceAnalysis.Services.Data.Trading
{
    using BinanceAnalysis.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public sealed class MarketPriceRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptions<MarketPriceRetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<MarketPriceRetentionService> logger)
        : BackgroundService
    {
        private readonly MarketPriceRetentionOptions settings = options.Value;

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            logger.LogInformation(
                "Market price retention service started. " +
                "Retention: {RetentionHours} hours. " +
                "Cleanup interval: {CleanupIntervalMinutes} minutes. " +
                "Batch size: {BatchSize}.",
                settings.RetentionHours,
                settings.CleanupIntervalMinutes,
                settings.BatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredTradesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "An error occurred while cleaning up expired market trades.");
                }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromMinutes(settings.CleanupIntervalMinutes),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            logger.LogInformation(
                "Market price retention service stopped.");
        }

        private async Task CleanupExpiredTradesAsync(
            CancellationToken cancellationToken)
        {
            var cutoff = timeProvider.GetUtcNow()
                .UtcDateTime
                .AddHours(-settings.RetentionHours);

            var totalDeleted = 0;

            await using var scope = scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            while (!cancellationToken.IsCancellationRequested)
            {
                // Retrieve only the IDs of the next batch.
                var batchIds = await dbContext.MarketPrices
                    .AsNoTracking()
                    .Where(x => x.ObservedAtUtc < cutoff)
                    .OrderBy(x => x.ObservedAtUtc)
                    .ThenBy(x => x.Id)
                    .Select(x => x.Id)
                    .Take(settings.BatchSize)
                    .ToListAsync(cancellationToken);

                if (batchIds.Count == 0)
                {
                    break;
                }

                // Delete the selected batch directly in SQL.
                var deleted = await dbContext.MarketPrices
                    .Where(x => batchIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);

                totalDeleted += deleted;
            }

            if (totalDeleted > 0)
            {
                logger.LogInformation(
                    "Deleted {DeletedCount} expired market trades. " +
                    "Retention cutoff: {CutoffUtc}.",
                    totalDeleted,
                    cutoff);
            }
            else
            {
                logger.LogDebug(
                    "No expired market trades found. Retention cutoff: {CutoffUtc}.",
                    cutoff);
            }
        }
    }
}
