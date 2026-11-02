namespace BinanceAnalysis.API.Services.Authentication
{
    using BinanceAnalysis.API.ViewModels.Authentication;

    public interface IJwtTokenService
    {
        TokenResponse CreateToken(string username);
    }
}
