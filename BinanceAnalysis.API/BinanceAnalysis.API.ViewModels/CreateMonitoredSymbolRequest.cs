namespace BinanceAnalysis.API.ViewModels.Symbols
{
    using System.ComponentModel.DataAnnotations;

    public class CreateMonitoredSymbolRequest
    {
        [Required]
        [StringLength(20, MinimumLength = 1)]
        [RegularExpression(
            "^[A-Za-z0-9]+$",
            ErrorMessage = "Symbol must contain only letters and numbers.")]
        public string Symbol { get; set; } = string.Empty;
    }
}