
namespace BinanceAnalysis.Console.Services
{
    using BinanceAnalysis.Console.Models;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;

    public class PriceAnalysisApiClient(HttpClient httpClient)
        : IPriceAnalysisApiClient
    {
        private readonly SemaphoreSlim authenticationLock = new(1, 1);

        private string? username;
        private string? password;
        private string? accessToken;

        public async Task<bool> AuthenticateAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            this.username = username;
            this.password = password;
            accessToken = null;

            await authenticationLock.WaitAsync(cancellationToken);

            try
            {
                accessToken = await RequestAccessTokenAsync(
                    cancellationToken);

                if (accessToken is null)
                {
                    this.username = null;
                    this.password = null;
                    return false;
                }

                return true;
            }
            finally
            {
                authenticationLock.Release();
            }
        }

        private async Task<string?> RequestAccessTokenAsync(
            CancellationToken cancellationToken)
        {
            using var response = await httpClient.PostAsJsonAsync(
                "api/auth/token",
                new
                {
                    Username = username,
                    Password = password
                },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var tokenProperty = document.RootElement
                .EnumerateObject()
                .FirstOrDefault(property =>
                    string.Equals(
                        property.Name,
                        "accessToken",
                        StringComparison.OrdinalIgnoreCase));

            if (tokenProperty.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "The authentication response does not contain a valid access token.");
            }

            var token = tokenProperty.Value.GetString();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "The authentication response contains an empty access token.");
            }

            return token;
        }

        private async Task<bool> RefreshAccessTokenAsync(
            string rejectedToken,
            CancellationToken cancellationToken)
        {
            await authenticationLock.WaitAsync(cancellationToken);

            try
            {
                // Another request may already have refreshed the token.
                if (accessToken != rejectedToken)
                {
                    return accessToken is not null;
                }

                var refreshedToken = await RequestAccessTokenAsync(
                    cancellationToken);

                if (refreshedToken is null)
                {
                    accessToken = null;
                    username = null;
                    password = null;
                    return false;
                }

                accessToken = refreshedToken;
                return true;
            }
            finally
            {
                authenticationLock.Release();
            }
        }

        private async Task<HttpResponseMessage> SendAuthenticatedAsync(
            Func<HttpRequestMessage> requestFactory,
            CancellationToken cancellationToken)
        {
            var currentToken = accessToken;

            if (string.IsNullOrWhiteSpace(currentToken))
            {
                throw new InvalidOperationException(
                    "You must authenticate before making API requests.");
            }

            using var request = requestFactory();

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", currentToken);

            var response = await httpClient.SendAsync(
                request,
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            response.Dispose();

            var refreshed = await RefreshAccessTokenAsync(
                currentToken,
                cancellationToken);

            if (!refreshed)
            {
                throw new HttpRequestException(
                    "Your administrator session has expired and reauthentication failed.",
                    null,
                    HttpStatusCode.Unauthorized);
            }

            var newToken = accessToken!;

            using var retryRequest = requestFactory();

            retryRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", newToken);

            // Retry exactly once. A second 401 is returned to the caller.
            return await httpClient.SendAsync(
                retryRequest,
                cancellationToken);
        }

        public async Task<decimal?> Get24HourAveragePriceAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            var encodedSymbol = Uri.EscapeDataString(symbol);

            using var response = await SendAuthenticatedAsync(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    $"api/{encodedSymbol}/24hAvgPrice"),
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
                    startDateTime.Value.ToString(
                        "O",
                        CultureInfo.InvariantCulture));

                url += $"&s={startDate}";
            }

            using var response = await SendAuthenticatedAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url),
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
            using var response = await SendAuthenticatedAsync(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    "api/admin/symbols"),
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
            using var response = await SendAuthenticatedAsync(
                () =>
                {
                    var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        "api/admin/symbols");

                    request.Content = JsonContent.Create(
                        new { Symbol = symbol });

                    return request;
                },
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

            using var response = await SendAuthenticatedAsync(
                () =>
                {
                    var request = new HttpRequestMessage(
                        HttpMethod.Put,
                        $"api/admin/symbols/{encodedSymbol}");

                    request.Content = JsonContent.Create(
                        new { IsActive = isActive });

                    return request;
                },
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

            using var response = await SendAuthenticatedAsync(
                () => new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"api/admin/symbols/{encodedSymbol}"),
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
