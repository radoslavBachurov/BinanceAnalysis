namespace BinanceAnalysis.Services.Data.Trading
{
    public sealed class MarketPriceRetentionOptions
    {
        public const string SectionName = "MarketPriceRetention";

        public int RetentionHours { get; set; } = 24;

        public int CleanupIntervalMinutes { get; set; } = 60;

        public int BatchSize { get; set; } = 1000;
    }
}
