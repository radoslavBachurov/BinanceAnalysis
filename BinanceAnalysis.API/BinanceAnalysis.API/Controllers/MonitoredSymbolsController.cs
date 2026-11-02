namespace BinanceAnalysis.API.Controllers.Admin
{
    using BinanceAnalysis.API.ViewModels.Symbols;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Symbols;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Provides administrative operations for monitored trading symbols.
    /// </summary>
    [ApiController]
    [Route("api/admin/symbols")]
    [Authorize(Roles = "Admin")]
    public class MonitoredSymbolsController(
        IMonitoredSymbolService monitoredSymbolService) : ControllerBase
    {
        /// <summary>
        /// Gets all configured monitored trading symbols.
        /// </summary>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>The collection of monitored symbols.</returns>
        /// <response code="200">Returns all monitored symbols.</response>
        [HttpGet]
        [ProducesResponseType(
            typeof(IReadOnlyCollection<MonitoredSymbol>),
            StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            CancellationToken cancellationToken)
        {
            var symbols = await monitoredSymbolService.GetAllAsync(
                cancellationToken);

            return Ok(symbols);
        }

        /// <summary>
        /// Adds a new trading symbol to the monitored symbols list.
        /// </summary>
        /// <param name="request">The trading symbol to add.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>The newly created monitored symbol.</returns>
        /// <response code="201">The symbol was successfully added.</response>
        /// <response code="400">The symbol is invalid or is not currently trading on Binance Spot.</response>
        /// <response code="409">The symbol is already being monitored.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
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
                        Message =
                            $"Symbol {symbol} does not exist on Binance Spot or is not currently trading."
                    }),

                _ => throw new InvalidOperationException(
                    "Unexpected monitored symbol result.")
            };
        }

        /// <summary>
        /// Activates or deactivates an existing monitored trading symbol.
        /// </summary>
        /// <param name="symbol">The trading symbol to update.</param>
        /// <param name="request">The requested active status.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <returns>The updated symbol and its active status.</returns>
        /// <response code="200">The symbol status was successfully updated.</response>
        /// <response code="404">The requested symbol was not found.</response>
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

        /// <summary>
        /// Deletes a monitored trading symbol.
        /// </summary>
        /// <param name="symbol">The trading symbol to delete.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="204">The symbol was successfully deleted.</response>
        /// <response code="404">The requested symbol was not found.</response>
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