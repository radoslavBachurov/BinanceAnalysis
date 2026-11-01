namespace BinanceAnalysis.API.Infrastructure.Binance.Validation
{
    public interface IBinanceSymbolValidationClient
    {
        Task<bool> IsValidTradingSymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default);
    }
}