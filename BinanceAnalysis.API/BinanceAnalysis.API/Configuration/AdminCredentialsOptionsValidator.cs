namespace BinanceAnalysis.API.Configuration
{
    using Microsoft.Extensions.Options;

    public sealed class AdminCredentialsOptionsValidator
        : IValidateOptions<AdminCredentialsOptions>
    {
        public ValidateOptionsResult Validate(
            string? name,
            AdminCredentialsOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Username))
            {
                return ValidateOptionsResult.Fail(
                    "Administrator username must be configured.");
            }

            if (string.IsNullOrWhiteSpace(options.Password))
            {
                return ValidateOptionsResult.Fail(
                    "Administrator password must be configured.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
