
namespace BinanceAnalysis.Console
{
    using System.Globalization;
    using System.Net.Http;
    using System.Text.Json;
    using BinanceAnalysis.Common;
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

        private static async Task RunMenuAsync(
            IPriceAnalysisApiClient apiClient)
        {
            while (true)
            {
                DisplayMenu();

                var choice = System.Console.ReadLine();

                if (choice is null or "0")
                {
                    return;
                }

                try
                {
                    await ExecuteMenuOptionAsync(choice, apiClient);
                }
                catch (HttpRequestException exception)
                {
                    DisplayApiError(exception);
                }
                catch (TaskCanceledException)
                {
                    System.Console.WriteLine(
                        "The API request timed out or was cancelled.");
                }
                catch (JsonException)
                {
                    System.Console.WriteLine(
                        "The API returned an unexpected response.");
                }
                catch (InvalidOperationException exception)
                {
                    System.Console.WriteLine(exception.Message);
                }
            }
        }

        private static void DisplayMenu()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("========================================");
            System.Console.WriteLine("       BINANCE PRICE ANALYSIS");
            System.Console.WriteLine("========================================");
            System.Console.WriteLine();
            System.Console.WriteLine("PRICE ANALYSIS");
            System.Console.WriteLine("1. Get 24-hour average price");
            System.Console.WriteLine("2. Get Simple Moving Average");
            System.Console.WriteLine();
            System.Console.WriteLine("MONITORED SYMBOL ADMINISTRATION");
            System.Console.WriteLine("3. List monitored symbols");
            System.Console.WriteLine("4. Add monitored symbol");
            System.Console.WriteLine("5. Activate monitored symbol");
            System.Console.WriteLine("6. Deactivate monitored symbol");
            System.Console.WriteLine("7. Delete monitored symbol");
            System.Console.WriteLine();
            System.Console.WriteLine("0. Exit");
            System.Console.WriteLine("========================================");
            System.Console.Write("Select an option: ");
        }

        private static async Task ExecuteMenuOptionAsync(
            string choice,
            IPriceAnalysisApiClient apiClient)
        {
            switch (choice)
            {
                case "1":
                    await Show24HourAverageAsync(apiClient);
                    break;

                case "2":
                    await ShowSimpleMovingAverageAsync(apiClient);
                    break;

                case "3":
                    await ShowMonitoredSymbolsAsync(apiClient);
                    break;

                case "4":
                    await AddMonitoredSymbolAsync(apiClient);
                    break;

                case "5":
                    await SetMonitoredSymbolStatusAsync(apiClient, true);
                    break;

                case "6":
                    await SetMonitoredSymbolStatusAsync(apiClient, false);
                    break;

                case "7":
                    await DeleteMonitoredSymbolAsync(apiClient);
                    break;

                default:
                    System.Console.WriteLine("Invalid menu option.");
                    break;
            }
        }

        private static void DisplayApiError(HttpRequestException exception)
        {
            var errorMessage = exception.Message;

            try
            {
                using var document = JsonDocument.Parse(exception.Message);

                if (document.RootElement.TryGetProperty(
                    "message",
                    out var messageElement))
                {
                    errorMessage = messageElement.GetString()
                        ?? exception.Message;
                }
            }
            catch (JsonException)
            {
                // The exception message is not a JSON response.
            }

            System.Console.WriteLine($"API request failed: {errorMessage}");
        }

        private static async Task Show24HourAverageAsync(
            IPriceAnalysisApiClient apiClient)
        {
            var symbol = await ReadSymbolAsync(apiClient);

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
                price.Value.ToString("N8", CultureInfo.InvariantCulture));
        }

        private static async Task ShowSimpleMovingAverageAsync(
            IPriceAnalysisApiClient apiClient)
        {
            var symbol = await ReadSymbolAsync(apiClient);

            if (symbol is null)
            {
                return;
            }

            var numberOfDataPoints = ReadNumberOfDataPoints();
            var timePeriod = ReadTimePeriod();

            if (!TryReadOptionalStartDateTime(out var startDateTime))
            {
                return;
            }

            var price = await apiClient.GetSimpleMovingAverageAsync(
                symbol,
                numberOfDataPoints,
                timePeriod,
                startDateTime);

            DisplaySimpleMovingAverageResult(symbol, price);
        }

        private static bool TryReadOptionalStartDateTime(
            out DateTime? startDateTime)
        {
            startDateTime = null;

            System.Console.WriteLine();
            System.Console.WriteLine(
                "Enter the optional start date and time in UTC.");
            System.Console.WriteLine(
                "Leave the date empty to use the default time window.");

            System.Console.Write("Start date (dd.MM.yyyy): ");
            var dateInput = System.Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(dateInput))
            {
                return true;
            }

            if (!DateTime.TryParseExact(
                dateInput,
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
            {
                System.Console.WriteLine(
                    "Invalid date. Expected format: dd.MM.yyyy.");

                return false;
            }

            System.Console.Write("Start time (HH:mm): ");
            var timeInput = System.Console.ReadLine()?.Trim();

            if (!DateTime.TryParseExact(
                timeInput,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedTime))
            {
                System.Console.WriteLine(
                    "Invalid time. Expected format: HH:mm.");

                return false;
            }

            startDateTime = DateTime.SpecifyKind(
                parsedDate.Date.Add(parsedTime.TimeOfDay),
                DateTimeKind.Utc);

            return true;
        }

        private static void DisplaySimpleMovingAverageResult(
            string symbol,
            decimal? price)
        {
            if (price is null)
            {
                System.Console.WriteLine(
                    $"Insufficient price data for {symbol} " +
                    "within the requested time window.");

                return;
            }

            System.Console.WriteLine(
                $"Simple Moving Average for {symbol}: " +
                price.Value.ToString("N8", CultureInfo.InvariantCulture));
        }

        private static async Task<string> ReadSymbolAsync(
            IPriceAnalysisApiClient apiClient)
        {
            var symbols = await apiClient.GetMonitoredSymbolsAsync();

            var activeSymbols = symbols
                .Where(x => x.IsActive)
                .OrderBy(x => x.Symbol)
                .ToList();

            if (activeSymbols.Count == 0)
            {
                throw new InvalidOperationException(
                    "There are no active monitored symbols.");
            }

            System.Console.WriteLine();
            System.Console.WriteLine("Available trading symbols:");

            for (var i = 0; i < activeSymbols.Count; i++)
            {
                System.Console.WriteLine(
                    $"{i + 1}. {activeSymbols[i].Symbol}");
            }

            while (true)
            {
                System.Console.Write("Select a symbol: ");

                if (int.TryParse(
                    System.Console.ReadLine(),
                    out var selection) &&
                    selection >= 1 &&
                    selection <= activeSymbols.Count)
                {
                    return activeSymbols[selection - 1].Symbol;
                }

                System.Console.WriteLine("Invalid selection. Try again.");
            }
        }

        private static int ReadNumberOfDataPoints()
        {
            while (true)
            {
                System.Console.Write(
                    $"Number of data points " +
                    $"({ProjectConstants.MinDataPoints}-" +
                    $"{ProjectConstants.MaxDataPoints}): ");

                if (int.TryParse(
                    System.Console.ReadLine(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var n) &&
                    n >= ProjectConstants.MinDataPoints &&
                    n <= ProjectConstants.MaxDataPoints)
                {
                    return n;
                }

                System.Console.WriteLine(
                    $"Please enter a number between " +
                    $"{ProjectConstants.MinDataPoints} and " +
                    $"{ProjectConstants.MaxDataPoints}.");
            }
        }

        private static string ReadTimePeriod()
        {
            var periods = new[]
            {
                ("1w", "1 Week"),
                ("1d", "1 Day"),
                ("30m", "30 Minutes"),
                ("5m", "5 Minutes"),
                ("1m", "1 Minute")
            };

            System.Console.WriteLine();
            System.Console.WriteLine("Available time periods:");

            for (var i = 0; i < periods.Length; i++)
            {
                System.Console.WriteLine(
                    $"{i + 1}. {periods[i].Item2}");
            }

            while (true)
            {
                System.Console.Write("Select a time period: ");

                if (int.TryParse(
                    System.Console.ReadLine(),
                    out var selection) &&
                    selection >= 1 &&
                    selection <= periods.Length)
                {
                    return periods[selection - 1].Item1;
                }

                System.Console.WriteLine("Invalid selection. Try again.");
            }
        }

        private static async Task ShowMonitoredSymbolsAsync(
            IPriceAnalysisApiClient apiClient)
        {
            var symbols = await apiClient.GetMonitoredSymbolsAsync();

            if (symbols.Count == 0)
            {
                System.Console.WriteLine("No monitored symbols found.");
                return;
            }

            System.Console.WriteLine();
            System.Console.WriteLine("MONITORED SYMBOLS");
            System.Console.WriteLine("----------------------------------------");

            foreach (var symbol in symbols.OrderBy(x => x.Symbol))
            {
                var status = symbol.IsActive ? "Active" : "Inactive";

                System.Console.WriteLine($"{symbol.Symbol,-15} {status}");
            }
        }

        private static async Task AddMonitoredSymbolAsync(
            IPriceAnalysisApiClient apiClient)
        {
            System.Console.Write("Enter trading symbol (e.g. SOLUSDT): ");

            var symbol = System.Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(symbol))
            {
                System.Console.WriteLine("Symbol cannot be empty.");
                return;
            }

            var added = await apiClient.AddMonitoredSymbolAsync(symbol);

            System.Console.WriteLine(
                added
                    ? $"Symbol {symbol} added successfully."
                    : $"Symbol {symbol} already exists.");
        }

        private static async Task SetMonitoredSymbolStatusAsync(
            IPriceAnalysisApiClient apiClient,
            bool isActive)
        {
            System.Console.Write("Enter trading symbol: ");

            var symbol = System.Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(symbol))
            {
                System.Console.WriteLine("Symbol cannot be empty.");
                return;
            }

            var symbols = await apiClient.GetMonitoredSymbolsAsync();

            var existingSymbol = symbols.FirstOrDefault(
                x => x.Symbol.Equals(
                    symbol,
                    StringComparison.OrdinalIgnoreCase));

            if (existingSymbol is null)
            {
                System.Console.WriteLine($"Symbol {symbol} was not found.");
                return;
            }

            if (existingSymbol.IsActive == isActive)
            {
                System.Console.WriteLine(
                    $"Symbol {symbol} is already " +
                    $"{(isActive ? "active" : "inactive")}.");

                return;
            }

            var updated = await apiClient.SetMonitoredSymbolStatusAsync(
                symbol,
                isActive);

            if (!updated)
            {
                System.Console.WriteLine(
                    $"Could not update symbol {symbol}.");

                return;
            }

            System.Console.WriteLine(
                $"Symbol {symbol} successfully " +
                $"{(isActive ? "activated" : "deactivated")}.");
        }

        private static async Task DeleteMonitoredSymbolAsync(
            IPriceAnalysisApiClient apiClient)
        {
            System.Console.Write("Enter trading symbol to delete: ");

            var symbol = System.Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(symbol))
            {
                System.Console.WriteLine("Symbol cannot be empty.");
                return;
            }

            var deleted = await apiClient.DeleteMonitoredSymbolAsync(symbol);

            System.Console.WriteLine(
                deleted
                    ? $"Symbol {symbol} deleted successfully."
                    : $"Symbol {symbol} was not found.");
        }
    }
}
