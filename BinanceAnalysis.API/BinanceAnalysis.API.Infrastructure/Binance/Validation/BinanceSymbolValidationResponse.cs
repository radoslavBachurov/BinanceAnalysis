namespace BinanceAnalysis.API.Infrastructure.Binance.Validation.Models
{
    using System.Text.Json.Serialization;

    public class BinanceSymbolValidationResponse
    {
        [JsonPropertyName("symbols")]
        public List<BinanceSymbolInfo> Symbols { get; set; } = [];
    }

    public class BinanceSymbolInfo
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}