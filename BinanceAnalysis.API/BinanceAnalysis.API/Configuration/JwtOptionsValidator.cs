namespace BinanceAnalysis.API.Configuration
{
    using System.Security.Cryptography;
    using Microsoft.Extensions.Options;

    public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        public ValidateOptionsResult Validate(
            string? name,
            JwtOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Issuer))
            {
                return ValidateOptionsResult.Fail(
                    "JWT issuer must be configured.");
            }

            if (string.IsNullOrWhiteSpace(options.Audience))
            {
                return ValidateOptionsResult.Fail(
                    "JWT audience must be configured.");
            }

            if (!TryDecodeSigningKey(options.SigningKey, out var keyBytes))
            {
                return ValidateOptionsResult.Fail(
                    "JWT signing key must be a valid Base64 string.");
            }

            if (keyBytes.Length < 32)
            {
                return ValidateOptionsResult.Fail(
                    "JWT signing key must contain at least 32 bytes.");
            }

            if (options.ExpirationMinutes is < 1 or > 60)
            {
                return ValidateOptionsResult.Fail(
                    "JWT expiration must be between 1 and 60 minutes.");
            }

            return ValidateOptionsResult.Success;
        }

        private static bool TryDecodeSigningKey(
            string signingKey,
            out byte[] keyBytes)
        {
            try
            {
                keyBytes = Convert.FromBase64String(signingKey);
                return true;
            }
            catch (FormatException)
            {
                keyBytes = [];
                return false;
            }
        }
    }
}
