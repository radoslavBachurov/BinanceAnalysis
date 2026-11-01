namespace BinanceAnalysis.API.Infrastructure.Binance.Validation
{
    using System.Net.Http.Json;
    using BinanceAnalysis.API.Infrastructure.Binance.Validation.Models;
    using Microsoft.Extensions.Logging;

    public class BinanceSymbolValidationClient(
        HttpClient httpClient,
        ILogger<BinanceSymbolValidationClient> logger)
        : IBinanceSymbolValidationClient
    {
        public async Task<bool> IsValidTradingSymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var response = await httpClient.GetAsync(
                $"api/v3/exchangeInfo?symbol={Uri.EscapeDataString(symbol)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Binance symbol validation returned HTTP {StatusCode} for {Symbol}.",
                    (int)response.StatusCode,
                    symbol);

                return false;
            }

            var result = await response.Content
                .ReadFromJsonAsync<BinanceSymbolValidationResponse>(
                    cancellationToken: cancellationToken);

            var symbolInfo = result?.Symbols?
                .FirstOrDefault(x => string.Equals(
                    x.Symbol,
                    symbol,
                    StringComparison.OrdinalIgnoreCase));

            return symbolInfo?.Status == "TRADING";
        }
    }
}