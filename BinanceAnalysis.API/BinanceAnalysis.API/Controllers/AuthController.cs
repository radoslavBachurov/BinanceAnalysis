namespace BinanceAnalysis.API.Controllers
{
    using BinanceAnalysis.API.Configuration;
    using BinanceAnalysis.API.ViewModels.Authentication;
    using BinanceAnalysis.API.Services.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using System.Security.Cryptography;
    using System.Text;

    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController(
        IOptions<AdminCredentialsOptions> credentialsOptions,
        IJwtTokenService jwtTokenService) : ControllerBase
    {
        private readonly AdminCredentialsOptions credentials =
            credentialsOptions.Value;

        /// <summary>
        /// Authenticates the administrator and returns a JWT access token.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("token")]
        [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<TokenResponse> CreateToken(
            [FromBody] TokenRequest request)
        {
            var usernameMatches = string.Equals(
                request.Username,
                credentials.Username,
                StringComparison.Ordinal);

            var suppliedPassword = Encoding.UTF8.GetBytes(
                request.Password);

            var configuredPassword = Encoding.UTF8.GetBytes(
                credentials.Password);

            var passwordMatches =
                CryptographicOperations.FixedTimeEquals(
                    suppliedPassword,
                    configuredPassword);

            if (!usernameMatches || !passwordMatches)
            {
                return Unauthorized();
            }

            var token = jwtTokenService.CreateToken(request.Username);

            return Ok(token);
        }
    }
}
