
namespace BinanceAnalysis.API.Services.Authentication
{
    using BinanceAnalysis.API.Configuration;
    using BinanceAnalysis.API.ViewModels.Authentication;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text;

    public sealed class JwtTokenService(
        IOptions<JwtOptions> jwtOptionsAccessor,
        TimeProvider timeProvider) : IJwtTokenService
    {
        private readonly JwtOptions jwtOptions = jwtOptionsAccessor.Value;

        public TokenResponse CreateToken(string username)
        {
            var now = timeProvider.GetUtcNow();

            var expiresAt = now.AddMinutes(
                jwtOptions.ExpirationMinutes);

            var claims = new[]
            {
                new Claim("sub", username),
                new Claim("name", username),
                new Claim("role", "Admin"),
                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString("N"))
            };

            var signingKey = new SymmetricSecurityKey(
                Convert.FromBase64String(jwtOptions.SigningKey));

            var signingCredentials = new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expiresAt.UtcDateTime,
                signingCredentials: signingCredentials);

            var accessToken = new JwtSecurityTokenHandler()
                .WriteToken(token);

            return new TokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = (int)TimeSpan.FromMinutes(
                    jwtOptions.ExpirationMinutes).TotalSeconds
            };
        }
    }
}
