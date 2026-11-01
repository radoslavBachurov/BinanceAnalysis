namespace BinanceAnalysis.Tests.Controllers
{
    using BinanceAnalysis.API.Controllers;
    using BinanceAnalysis.Common;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using Xunit;

    public class PriceAnalysisControllerTests
    {
        private readonly Mock<IPriceAnalysisService> priceAnalysisService;
        private readonly PriceAnalysisController controller;

        public PriceAnalysisControllerTests()
        {
            this.priceAnalysisService = new Mock<IPriceAnalysisService>();

            this.controller = new PriceAnalysisController(
                this.priceAnalysisService.Object);
        }

        [Fact]
        public async Task Get24HourAveragePrice_ReturnsOk_WhenDataExists()
        {
            // Arrange
            this.priceAnalysisService
                .Setup(x => x.Get24HourAveragePriceAsync(
                    "BTCUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(77648.02m);

            // Act
            var result = await this.controller.Get24HourAveragePrice(
                "BTCUSDT",
                CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(77648.02m, okResult.Value);
        }

        [Fact]
        public async Task Get24HourAveragePrice_ReturnsNotFound_WhenNoDataExists()
        {
            // Arrange
            this.priceAnalysisService
                .Setup(x => x.Get24HourAveragePriceAsync(
                    "BTCUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);

            // Act
            var result = await this.controller.Get24HourAveragePrice(
                "BTCUSDT",
                CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetSimpleMovingAverage_ReturnsBadRequest_WhenNIsInvalid(
            int n)
        {
            // Act
            var result = await this.controller.GetSimpleMovingAverage(
                "BTCUSDT",
                n,
                "1m",
                null,
                CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);

            this.priceAnalysisService.Verify(
                x => x.GetSimpleMovingAverageAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetSimpleMovingAverage_ReturnsBadRequest_WhenPeriodIsUnsupported()
        {
            // Act
            var result = await this.controller.GetSimpleMovingAverage(
                "BTCUSDT",
                10,
                "1h",
                null,
                CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetSimpleMovingAverage_ReturnsBadRequest_WhenStartDateIsInvalid()
        {
            // Act
            var result = await this.controller.GetSimpleMovingAverage(
                "BTCUSDT",
                10,
                "1m",
                "not-a-date",
                CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetSimpleMovingAverage_ReturnsNotFound_WhenInsufficientDataExists()
        {
            // Arrange
            this.priceAnalysisService
                .Setup(x => x.GetSimpleMovingAverageAsync(
                    "BTCUSDT",
                    10,
                    TimeSpan.FromMinutes(1),
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);

            // Act
            var result = await this.controller.GetSimpleMovingAverage(
                "BTCUSDT",
                10,
                "1m",
                null,
                CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetSimpleMovingAverage_ReturnsOk_WhenCalculationSucceeds()
        {
            // Arrange
            this.priceAnalysisService
                .Setup(x => x.GetSimpleMovingAverageAsync(
                    "BTCUSDT",
                    10,
                    TimeSpan.FromMinutes(1),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(77635.25m);

            // Act
            var result = await this.controller.GetSimpleMovingAverage(
                "BTCUSDT",
                10,
                "1m",
                "2026-10-31T17:00:00Z",
                CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(77635.25m, okResult.Value);
        }

        [Fact]
        public async Task GetSimpleMovingAverage_ReturnsBadRequest_WhenNExceedsMaximum()
        {
            var result = await this.controller.GetSimpleMovingAverage(
                "BTCUSDT",
                ProjectConstants.MaxDataPoints + 1,
                "1m",
                null,
                CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}