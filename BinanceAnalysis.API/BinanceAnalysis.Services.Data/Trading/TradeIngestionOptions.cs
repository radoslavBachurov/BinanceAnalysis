
namespace BinanceAnalysis.Services.Data.Trading
{
    public class TradeIngestionOptions
    {
        public const string SectionName = "TradeIngestion";

        public bool Enabled { get; set; } = true;

        public int ChannelCapacity { get; set; } = 5000;

        public int BatchSize { get; set; } = 500;

        public int FlushIntervalSeconds { get; set; } = 1;

        public int InitialReconnectDelaySeconds { get; set; } = 1;

        public int MaximumReconnectDelaySeconds { get; set; } = 30;

        public int SymbolRefreshIntervalSeconds { get; set; } = 10;

        public int StableConnectionSeconds { get; set; } = 30;
    }
}
