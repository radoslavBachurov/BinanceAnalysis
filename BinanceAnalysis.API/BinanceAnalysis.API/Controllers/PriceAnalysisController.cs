namespace BinanceAnalysis.API.Controllers
{
    using System.Globalization;
    using BinanceAnalysis.Common;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Provides price analysis operations for monitored trading symbols.
    /// </summary>
    [ApiController]
    [Route("api/{symbol}")]
    [Produces("application/json", "application/xml")]
    public class PriceAnalysisController : ControllerBase
    {
        private readonly IPriceAnalysisService priceAnalysisService;

        public PriceAnalysisController(
            IPriceAnalysisService priceAnalysisService)
        {
            this.priceAnalysisService = priceAnalysisService;
        }

        /// <summary>
        /// Gets the average trade price for a symbol over the last 24 hours.
        /// </summary>
        /// <param name="symbol">The trading symbol, for example BTCUSDT.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>The 24-hour average price.</returns>
        /// <response code="200">Returns the calculated average price.</response>
        /// <response code="404">No price data is available for the symbol in the last 24 hours.</response>
        [HttpGet("24hAvgPrice")]
        [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get24HourAveragePrice(
            string symbol,
            CancellationToken cancellationToken)
        {
            var normalizedSymbol = symbol.ToUpperInvariant();

            var averagePrice =
                await this.priceAnalysisService.Get24HourAveragePriceAsync(
                    normalizedSymbol,
                    cancellationToken);

            if (averagePrice is null)
            {
                return this.NotFound(
                    $"No price data found for {normalizedSymbol} in the last 24 hours.");
            }

            return this.Ok(averagePrice.Value);
        }

        /// <summary>
        /// Calculates the simple moving average for a trading symbol.
        /// </summary>
        /// <param name="symbol">The trading symbol, for example BTCUSDT.</param>
        /// <param name="n">
        /// Number of trade records to include in the calculation.
        /// Must be between 1 and 10,000.
        /// </param>
        /// <param name="p">
        /// Time window. Supported values: 1w, 1d, 30m, 5m, 1m.
        /// </param>
        /// <param name="s">
        /// Optional UTC start date and time in ISO 8601 format.
        /// Example: 2026-11-01T14:30:00Z.
        /// When omitted, the time window is calculated backwards
        /// from the current UTC time. When supplied, the calculation
        /// uses the interval beginning at this time and ending after
        /// the selected period.
        /// </param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>The calculated simple moving average.</returns>
        /// <remarks>
        /// Example:
        /// GET /api/BTCUSDT/SimpleMovingAverage?n=10&amp;p=5m
        ///
        /// With a start date:
        /// GET /api/BTCUSDT/SimpleMovingAverage?n=10&amp;p=5m&amp;s=2026-10-31T12:00:00Z
        /// </remarks>
        /// <response code="200">Returns the calculated simple moving average.</response>
        /// <response code="400">One or more query parameters are invalid.</response>
        /// <response code="404">Insufficient price data is available for the calculation.</response>
        [HttpGet("SimpleMovingAverage")]
        [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSimpleMovingAverage(
            string symbol,
            [FromQuery] int? n,
            [FromQuery] string? p,
            [FromQuery] string? s,
            CancellationToken cancellationToken)
        {
            if (n is null ||
                n < ProjectConstants.MinDataPoints ||
                n > ProjectConstants.MaxDataPoints)
            {
                return this.BadRequest(
                    $"Number of data points must be between " +
                    $"{ProjectConstants.MinDataPoints} and " +
                    $"{ProjectConstants.MaxDataPoints}.");
            }

            if (!TryParsePeriod(p, out var period))
            {
                return this.BadRequest(
                    "Parameter 'p' must be one of: 1w, 1d, 30m, 5m, 1m.");
            }

            DateTime? startDateTime = null;

            if (!string.IsNullOrWhiteSpace(s))
            {
                if (!DateTimeOffset.TryParse(
                    s,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out var parsedDateTime))
                {
                    return this.BadRequest(
                        "Parameter 's' must be a valid date/time in ISO 8601 format.");
                }

                startDateTime = parsedDateTime.UtcDateTime;
            }

            var average = await this.priceAnalysisService
                .GetSimpleMovingAverageAsync(
                    symbol.ToUpperInvariant(),
                    n.Value,
                    period,
                    startDateTime,
                    cancellationToken);

            if (average is null)
            {
                return this.NotFound(
                    $"Insufficient price data for {symbol} " +
                    $"to calculate an SMA using {n} data points.");
            }

            return this.Ok(average.Value);
        }

        private static bool TryParsePeriod(
            string? value,
            out TimeSpan period)
        {
            period = value switch
            {
                "1w" => TimeSpan.FromDays(7),
                "1d" => TimeSpan.FromDays(1),
                "30m" => TimeSpan.FromMinutes(30),
                "5m" => TimeSpan.FromMinutes(5),
                "1m" => TimeSpan.FromMinutes(1),
                _ => TimeSpan.Zero
            };

            return period > TimeSpan.Zero;
        }
    }
}