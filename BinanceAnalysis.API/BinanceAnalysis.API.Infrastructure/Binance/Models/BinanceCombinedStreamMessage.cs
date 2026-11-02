
namespace BinanceAnalysis.API.Infrastructure.Binance.Models
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class BinanceCombinedStreamMessage
    {
        [JsonPropertyName("stream")]
        public string Stream { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public BinanceTradeEvent? Data { get; set; }

        [JsonPropertyName("result")]
        public JsonElement Result { get; set; }

        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("error")]
        public BinanceSubscriptionError? Error { get; set; }
    }

    public class BinanceSubscriptionError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
}
