namespace BinanceAnalysis.Console
{
    using System.Globalization;
    using System.Net.Http;
    using BinanceAnalysis.Console.Services;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var apiBaseUrl = configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "API base URL is not configured.");

            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp &&
                 baseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "API base URL must be a valid HTTP or HTTPS URL.");
            }

            var services = new ServiceCollection();

            services.AddHttpClient<IPriceAnalysisApiClient, PriceAnalysisApiClient>(
                client =>
                {
                    client.BaseAddress = baseUri;
                    client.Timeout = TimeSpan.FromSeconds(30);
                });

            using var serviceProvider = services.BuildServiceProvider();

            var apiClient = serviceProvider
                .GetRequiredService<IPriceAnalysisApiClient>();

            await RunMenuAsync(apiClient);
        }

        private static async Task RunMenuAsync(IPriceAnalysisApiClient apiClient)
        {
            while (true)
            {
                System.Console.WriteLine();
                System.Console.WriteLine("=================================");
                System.Console.WriteLine("     BINANCE PRICE ANALYSIS");
                System.Console.WriteLine("=================================");
                System.Console.WriteLine("1. Get 24-hour average price");
                System.Console.WriteLine("2. Get Simple Moving Average");
                System.Console.WriteLine("0. Exit");
                System.Console.Write("Select an option: ");

                var choice = System.Console.ReadLine();

                if (choice == "0" || choice is null)
                {
                    return;
                }

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await Show24HourAverageAsync(apiClient);
                            break;

                        case "2":
                            await ShowSimpleMovingAverageAsync(apiClient);
                            break;

                        default:
                            System.Console.WriteLine("Invalid menu option.");
                            break;
                    }
                }
                catch (HttpRequestException exception)
                {
                    System.Console.WriteLine(
                        $"API request failed: {exception.Message}");
                }
                catch (TaskCanceledException)
                {
                    System.Console.WriteLine(
                        "The API request timed out or was cancelled.");
                }
                catch (System.Text.Json.JsonException)
                {
                    System.Console.WriteLine(
                        "The API returned an unexpected response.");
                }
            }
        }

        private static async Task Show24HourAverageAsync(
            IPriceAnalysisApiClient apiClient)
        {
            var symbol = ReadSymbol();

            if (symbol is null)
            {
                return;
            }

            var price = await apiClient.Get24HourAveragePriceAsync(symbol);

            if (price is null)
            {
                System.Console.WriteLine(
                    $"No price data available for {symbol}.");
                return;
            }

            System.Console.WriteLine(
                $"24-hour average price for {symbol}: " +
                $"{price.Value.ToString("N8", CultureInfo.InvariantCulture)}");
        }

        private static async Task ShowSimpleMovingAverageAsync(
            IPriceAnalysisApiClient apiClient)
        {
            var symbol = ReadSymbol();

            if (symbol is null)
            {
                return;
            }

            System.Console.Write("Number of data points (n): ");

            if (!int.TryParse(System.Console.ReadLine(), out var n) || n <= 0)
            {
                System.Console.WriteLine(
                    "Number of data points must be a positive integer.");
                return;
            }

            System.Console.Write("Time period (1w, 1d, 30m, 5m, 1m): ");

            var period = System.Console.ReadLine()?.Trim().ToLowerInvariant();

            if (period is not ("1w" or "1d" or "30m" or "5m" or "1m"))
            {
                System.Console.WriteLine("Unsupported time period.");
                return;
            }

            System.Console.Write(
                "Start date/time in UTC (optional, e.g. 2026-11-01T10:00:00Z): ");

            var startInput = System.Console.ReadLine()?.Trim();

            DateTime? startDateTime = null;

            if (!string.IsNullOrWhiteSpace(startInput))
            {
                if (!DateTimeOffset.TryParse(
                        startInput,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal |
                        DateTimeStyles.AdjustToUniversal,
                        out var parsedDate))
                {
                    System.Console.WriteLine(
                        "Invalid start date/time.");
                    return;
                }

                startDateTime = parsedDate.UtcDateTime;
            }

            var price = await apiClient.GetSimpleMovingAverageAsync(
                symbol,
                n,
                period,
                startDateTime);

            if (price is null)
            {
                System.Console.WriteLine(
                    $"Insufficient price data for {symbol} " +
                    "within the requested time window.");
                return;
            }

            System.Console.WriteLine(
                $"Simple Moving Average for {symbol}: " +
                $"{price.Value.ToString("N8", CultureInfo.InvariantCulture)}");
        }

        private static string? ReadSymbol()
        {
            System.Console.Write("Enter symbol (e.g. BTCUSDT): ");

            var symbol = System.Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(symbol))
            {
                System.Console.WriteLine("Symbol is required.");
                return null;
            }

            return symbol;
        }
    }
}