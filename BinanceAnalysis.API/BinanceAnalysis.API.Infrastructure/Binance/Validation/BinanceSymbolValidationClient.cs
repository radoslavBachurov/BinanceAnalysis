
namespace BinanceAnalysis.API.Infrastructure.Binance.Validation
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;
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
            using var response = await httpClient.GetAsync(
                $"api/v3/exchangeInfo?symbol={Uri.EscapeDataString(symbol)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await using var stream = await response.Content
                    .ReadAsStreamAsync(cancellationToken);

                using var errorDocument = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);

                if (errorDocument.RootElement.TryGetProperty(
                        "code",
                        out var codeElement) &&
                    codeElement.TryGetInt32(out var errorCode) &&
                    errorCode == -1121)
                {
                    logger.LogInformation(
                        "Binance rejected {Symbol} because it is an invalid symbol.",
                        symbol);

                    return false;
                }
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<BinanceSymbolValidationResponse>(
                    cancellationToken: cancellationToken);

            var symbolInfo = result?.Symbols?
                .FirstOrDefault(x => string.Equals(
                    x.Symbol,
                    symbol,
                    StringComparison.OrdinalIgnoreCase));

            var isTrading = symbolInfo?.Status == "TRADING";

            if (!isTrading)
            {
                logger.LogInformation(
                    "Binance symbol {Symbol} is not currently in TRADING status.",
                    symbol);
            }

            return isTrading;
        }
    }
}
