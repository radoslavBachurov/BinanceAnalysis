namespace BinanceAnalysis.Tests.Controllers
{
    using BinanceAnalysis.API.Controllers.Admin;
    using BinanceAnalysis.API.ViewModels.Symbols;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Symbols;
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using Xunit;

    public class MonitoredSymbolsControllerTests
    {
        private readonly Mock<IMonitoredSymbolService> monitoredSymbolService;
        private readonly MonitoredSymbolsController controller;

        public MonitoredSymbolsControllerTests()
        {
            this.monitoredSymbolService = new Mock<IMonitoredSymbolService>();

            this.controller = new MonitoredSymbolsController(
                this.monitoredSymbolService.Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOk_WithConfiguredSymbols()
        {
            // Arrange
            IReadOnlyCollection<MonitoredSymbol> symbols =
            [
                new MonitoredSymbol
                {
                    Symbol = "BTCUSDT",
                    IsActive = true
                },
                new MonitoredSymbol
                {
                    Symbol = "ADAUSDT",
                    IsActive = true
                }
            ];

            this.monitoredSymbolService
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(symbols);

            // Act
            var result = await this.controller.GetAll(CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedSymbols =
                Assert.IsAssignableFrom<IReadOnlyCollection<MonitoredSymbol>>(
                    okResult.Value);

            Assert.Equal(2, returnedSymbols.Count);
        }

        [Fact]
        public async Task Add_ReturnsCreated_WhenSymbolIsNew()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.AddAsync(
                    "SOLUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(AddMonitoredSymbolResult.Added);

            var request = new CreateMonitoredSymbolRequest
            {
                Symbol = "solusdt"
            };

            // Act
            var result = await this.controller.Add(
                request,
                CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<CreatedResult>(result);
            Assert.Equal("/api/admin/symbols", createdResult.Location);

            this.monitoredSymbolService.Verify(
                x => x.AddAsync("SOLUSDT", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Add_ReturnsConflict_WhenSymbolAlreadyExists()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.AddAsync(
                    "BTCUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(AddMonitoredSymbolResult.AlreadyExists);

            var request = new CreateMonitoredSymbolRequest
            {
                Symbol = "BTCUSDT"
            };

            // Act
            var result = await this.controller.Add(
                request,
                CancellationToken.None);

            // Assert
            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task Add_ReturnsBadRequest_WhenBinanceSymbolIsInvalid()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.AddAsync(
                    "INVALIDUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(AddMonitoredSymbolResult.InvalidBinanceSymbol);

            var request = new CreateMonitoredSymbolRequest
            {
                Symbol = "INVALIDUSDT"
            };

            // Act
            var result = await this.controller.Add(
                request,
                CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetActiveStatus_ReturnsOk_WhenSymbolExists()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.SetActiveStatusAsync(
                    "SOLUSDT",
                    false,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SetMonitoredSymbolStatusRequest
            {
                IsActive = false
            };

            // Act
            var result = await this.controller.SetActiveStatus(
                "solusdt",
                request,
                CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            this.monitoredSymbolService.Verify(
                x => x.SetActiveStatusAsync(
                    "SOLUSDT",
                    false,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SetActiveStatus_ReturnsNotFound_WhenSymbolDoesNotExist()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.SetActiveStatusAsync(
                    "UNKNOWNUSDT",
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new SetMonitoredSymbolStatusRequest
            {
                IsActive = true
            };

            // Act
            var result = await this.controller.SetActiveStatus(
                "UNKNOWNUSDT",
                request,
                CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Delete_ReturnsNoContent_WhenSymbolExists()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.DeleteAsync(
                    "SOLUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await this.controller.Delete(
                "solusdt",
                CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);

            this.monitoredSymbolService.Verify(
                x => x.DeleteAsync("SOLUSDT", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Delete_ReturnsNotFound_WhenSymbolDoesNotExist()
        {
            // Arrange
            this.monitoredSymbolService
                .Setup(x => x.DeleteAsync(
                    "UNKNOWNUSDT",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await this.controller.Delete(
                "UNKNOWNUSDT",
                CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}