namespace BinanceAnalysis.Console.Services
{
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Json;

    public class PriceAnalysisApiClient(HttpClient httpClient)
        : IPriceAnalysisApiClient
    {
        public async Task<decimal?> Get24HourAveragePriceAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var encodedSymbol = Uri.EscapeDataString(symbol);

            using var response = await httpClient.GetAsync(
                $"api/{encodedSymbol}/24hAvgPrice",
                cancellationToken);

            return await ReadPriceResponseAsync(response, cancellationToken);
        }

        public async Task<decimal?> GetSimpleMovingAverageAsync(
            string symbol,
            int numberOfDataPoints,
            string timePeriod,
            DateTime? startDateTime = null,
            CancellationToken cancellationToken = default)
        {
            var encodedSymbol = Uri.EscapeDataString(symbol);
            var encodedPeriod = Uri.EscapeDataString(timePeriod);

            var url =
                $"api/{encodedSymbol}/SimpleMovingAverage" +
                $"?n={numberOfDataPoints}&p={encodedPeriod}";

            if (startDateTime.HasValue)
            {
                var startDate = Uri.EscapeDataString(
                    startDateTime.Value.ToString("O", CultureInfo.InvariantCulture));

                url += $"&s={startDate}";
            }

            using var response = await httpClient.GetAsync(
                url,
                cancellationToken);

            return await ReadPriceResponseAsync(response, cancellationToken);
        }

        private static async Task<decimal?> ReadPriceResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<decimal>(
                cancellationToken: cancellationToken);
        }
    }
}