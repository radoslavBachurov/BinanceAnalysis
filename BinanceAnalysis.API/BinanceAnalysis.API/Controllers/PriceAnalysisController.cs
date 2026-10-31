namespace BinanceAnalysis.API.Controllers
{
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.AspNetCore.Mvc;
    using System.Globalization;

    [ApiController]
    [Route("api/{symbol}")]
    public class PriceAnalysisController : ControllerBase
    {
        private readonly IPriceAnalysisService priceAnalysisService;

        public PriceAnalysisController(IPriceAnalysisService priceAnalysisService)
        {
            this.priceAnalysisService = priceAnalysisService;
        }

        [HttpGet("24hAvgPrice")]
        public async Task<IActionResult> Get24HourAveragePrice(
            string symbol,
            CancellationToken cancellationToken)
        {
            var averagePrice =
                await this.priceAnalysisService.Get24HourAveragePriceAsync(
                    symbol.ToUpperInvariant(),
                    cancellationToken);

            if (averagePrice is null)
            {
                return this.NotFound(
                    $"No price data found for {symbol} in the last 24 hours.");
            }

            return this.Ok(averagePrice.Value);
        }

        [HttpGet("SimpleMovingAverage")]
        public async Task<IActionResult> GetSimpleMovingAverage(
            string symbol,
            [FromQuery] int? n,
            [FromQuery] string? p,
            [FromQuery] string? s,
            CancellationToken cancellationToken)
        {
            if (n is null || n <= 0)
            {
                return this.BadRequest(
                    "Parameter 'n' must be a positive integer.");
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