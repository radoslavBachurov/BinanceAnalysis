
namespace BinanceAnalysis.API.Infrastructure.Binance.Models
{
    using System.Text.Json.Serialization;

    public class BinanceCombinedStreamMessage
    {
        [JsonPropertyName("stream")]
        public string Stream { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public BinanceTradeEvent? Data { get; set; }
    }
}
