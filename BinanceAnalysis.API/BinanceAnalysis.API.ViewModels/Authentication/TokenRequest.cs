namespace BinanceAnalysis.API.ViewModels.Authentication
{
    using System.ComponentModel.DataAnnotations;

    public sealed class TokenRequest
    {
        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string Password { get; set; } = string.Empty;
    }
}
