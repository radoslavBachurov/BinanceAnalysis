namespace BinanceAnalysis.API.Configuration
{
    public sealed class AdminCredentialsOptions
    {
        public const string SectionName = "AdminCredentials";

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
