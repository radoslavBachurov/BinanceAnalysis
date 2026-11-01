namespace BinanceAnalysis.API.Controllers.Admin
{
    using BinanceAnalysis.API.ViewModels.Symbols;
    using BinanceAnalysis.Services.Data.Symbols;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/admin/symbols")]
    public class MonitoredSymbolsController(IMonitoredSymbolService monitoredSymbolService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            CancellationToken cancellationToken)
        {
            var symbols = await monitoredSymbolService.GetAllAsync(
                cancellationToken);

            return Ok(symbols);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Add(
        [FromBody] CreateMonitoredSymbolRequest request,
        CancellationToken cancellationToken)
        {
            var symbol = request.Symbol.Trim().ToUpperInvariant();

            var result = await monitoredSymbolService.AddAsync(
                symbol,
                cancellationToken);

            return result switch
            {
                AddMonitoredSymbolResult.Added =>
                    Created(
                        "/api/admin/symbols",
                        new
                        {
                            Symbol = symbol,
                            IsActive = true
                        }),

                AddMonitoredSymbolResult.AlreadyExists =>
                    Conflict(new
                    {
                        Message = $"Symbol {symbol} is already monitored."
                    }),

                AddMonitoredSymbolResult.InvalidBinanceSymbol =>
                    BadRequest(new
                    {
                        Message = $"Symbol {symbol} does not exist on Binance Spot or is not currently trading."
                    }),

                _ => throw new InvalidOperationException(
                    "Unexpected monitored symbol result.")
            };
        }

        [HttpPut("{symbol}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetActiveStatus(
            string symbol,
            [FromBody] SetMonitoredSymbolStatusRequest request,
            CancellationToken cancellationToken)
        {
            var normalizedSymbol = symbol.Trim().ToUpperInvariant();

            var updated = await monitoredSymbolService.SetActiveStatusAsync(
                normalizedSymbol,
                request.IsActive,
                cancellationToken);

            if (!updated)
            {
                return NotFound(new
                {
                    message = $"Symbol '{normalizedSymbol}' was not found."
                });
            }

            return Ok(new
            {
                symbol = normalizedSymbol,
                isActive = request.IsActive
            });
        }

        [HttpDelete("{symbol}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            string symbol,
            CancellationToken cancellationToken)
        {
            var normalizedSymbol = symbol.Trim().ToUpperInvariant();

            var deleted = await monitoredSymbolService.DeleteAsync(
                normalizedSymbol,
                cancellationToken);

            if (!deleted)
            {
                return NotFound(new
                {
                    message = $"Symbol '{normalizedSymbol}' was not found."
                });
            }

            return NoContent();
        }
    }
}