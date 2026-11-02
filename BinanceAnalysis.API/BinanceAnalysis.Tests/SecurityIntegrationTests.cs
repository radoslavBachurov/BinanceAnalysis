
namespace BinanceAnalysis.Tests
{
    using BinanceAnalysis.Tests.Infrastructure;
    using Microsoft.IdentityModel.Tokens;
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class SecurityIntegrationTests
        : IClassFixture<CustomWebApplicationFactory>
    {
        private const string Issuer = "BinanceAnalysis.API";
        private const string Audience = "BinanceAnalysis.Client";

        // Test-only key. Must match appsettings.IntegrationTests.json.
        private const string SigningKey =
            "dGVzdC1vbmx5LXNpZ25pbmcta2V5LWZvci1qd3QtdGVzdHMtMzItYnl0ZXM=";

        private readonly HttpClient client;

        public SecurityIntegrationTests(CustomWebApplicationFactory factory)
        {
            client = factory.CreateClient();
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsAccessToken()
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/token",
                new
                {
                    username = "admin",
                    password = "admin"
                });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>();

            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
            Assert.Equal("Bearer", body.TokenType);
            Assert.True(body.ExpiresIn > 0);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/token",
                new
                {
                    username = "admin",
                    password = "wrong-password"
                });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AdminEndpoint_WithoutToken_ReturnsUnauthorized()
        {
            var response = await client.GetAsync("/api/admin/symbols");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AdminEndpoint_WithInvalidToken_ReturnsUnauthorized()
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

            var response = await client.GetAsync("/api/admin/symbols");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AdminEndpoint_WithNonAdminRole_ReturnsForbidden()
        {
            SetBearerToken(CreateToken("User"));

            var response = await client.GetAsync("/api/admin/symbols");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AdminEndpoint_WithAdminRole_ReturnsOk()
        {
            SetBearerToken(CreateToken("Admin"));

            var response = await client.GetAsync("/api/admin/symbols");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PublicPriceEndpoint_WithoutToken_IsAccessible()
        {
            client.DefaultRequestHeaders.Authorization = null;

            var response = await client.GetAsync(
                "/api/BTCUSDT/24hAvgPrice");

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private void SetBearerToken(string token)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        private static string CreateToken(string role)
        {
            var key = Convert.FromBase64String(SigningKey);

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, "integration-test"),
                    new Claim("name", "integration-test"),
                    new Claim("role", role)
                ],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private sealed class TokenResponse
        {
            public string AccessToken { get; set; } = string.Empty;

            public string TokenType { get; set; } = string.Empty;

            public int ExpiresIn { get; set; }
        }
    }
}
