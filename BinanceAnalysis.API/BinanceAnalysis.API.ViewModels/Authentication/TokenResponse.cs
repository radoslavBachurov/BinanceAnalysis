namespace BinanceAnalysis.API.ViewModels.Authentication
{
    public sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = "Bearer";

        public int ExpiresIn { get; set; }
    }
}
