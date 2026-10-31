
namespace BinanceAnalysis.API.Infrastructure.Binance.Models
{
    using System.Text.Json.Serialization;

    public class BinanceTradeEvent
    {
        [JsonPropertyName("e")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("E")]
        public long EventTime { get; set; }

        [JsonPropertyName("s")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("t")]
        public long TradeId { get; set; }

        [JsonPropertyName("p")]
        public string Price { get; set; } = string.Empty;

        [JsonPropertyName("q")]
        public string Quantity { get; set; } = string.Empty;

        [JsonPropertyName("T")]
        public long TradeTime { get; set; }

        [JsonPropertyName("m")]
        public bool IsBuyerMarketMaker { get; set; }
    }
}
