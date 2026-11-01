namespace BinanceAnalysis.Tests.Controllers
{
    using System.ComponentModel.DataAnnotations;
    using BinanceAnalysis.API.ViewModels.Symbols;
    using Xunit;

    public class MonitoredSymbolRequestTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("SOL-USDT")]
        [InlineData("SOL USDT")]
        [InlineData("SOL.USDT")]
        public void CreateMonitoredSymbolRequest_IsInvalid_WhenSymbolIsInvalid(
            string symbol)
        {
            // Arrange
            var request = new CreateMonitoredSymbolRequest
            {
                Symbol = symbol
            };

            var validationResults = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(validationResults);
        }

        [Fact]
        public void CreateMonitoredSymbolRequest_IsValid_WhenSymbolIsValid()
        {
            // Arrange
            var request = new CreateMonitoredSymbolRequest
            {
                Symbol = "SOLUSDT"
            };

            var validationResults = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
    }
}