namespace BinanceAnalysis.Console.Services
{
    using BinanceAnalysis.Console.Models;
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

        public async Task<IReadOnlyList<MonitoredSymbolDto>> GetMonitoredSymbolsAsync(
            CancellationToken cancellationToken = default)
        {
            using var response = await httpClient.GetAsync(
                "api/admin/symbols",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<MonitoredSymbolDto>>(
                cancellationToken: cancellationToken)
                ?? [];
        }

        public async Task<bool> AddMonitoredSymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default)
        {
            using var response = await httpClient.PostAsJsonAsync(
                "api/admin/symbols",
                new { Symbol = symbol },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return false;
            }

            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(error)
                    ? $"API request failed with status code {(int)response.StatusCode}."
                    : error,
                null,
                response.StatusCode);
        }

        public async Task<bool> SetMonitoredSymbolStatusAsync(
            string symbol,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            var encodedSymbol = Uri.EscapeDataString(symbol);

            using var response = await httpClient.PutAsJsonAsync(
                $"api/admin/symbols/{encodedSymbol}",
                new { IsActive = isActive },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();

            return true;
        }

        public async Task<bool> DeleteMonitoredSymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var encodedSymbol = Uri.EscapeDataString(symbol);

            using var response = await httpClient.DeleteAsync(
                $"api/admin/symbols/{encodedSymbol}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();

            return true;
        }
    }
}