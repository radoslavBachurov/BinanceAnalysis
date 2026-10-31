namespace BinanceAnalysis.Data.Models
{
    public class MarketPrice
    {
        public long Id { get; set; }

        public string Symbol { get; set; } = string.Empty;

        public long TradeId { get; set; }

        public decimal Price { get; set; }

        public DateTime ObservedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
