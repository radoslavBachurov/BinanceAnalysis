
namespace BinanceAnalysis.Tests.Infrastructure
{
    using BinanceAnalysis.API;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public sealed class CustomWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTests");

            var testSettingsPath = Path.Combine(
                AppContext.BaseDirectory,
                "appsettings.IntegrationTests.json");

            var testConfiguration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(
                    "appsettings.IntegrationTests.json",
                    optional: false,
                    reloadOnChange: false)
                .Build();

            var connectionString =
                testConfiguration.GetConnectionString("IntegrationTestConnection")
                ?? throw new InvalidOperationException(
                    "IntegrationTestConnection was not configured.");

            // Protect against accidentally running tests against the development database.
            var connectionStringBuilder =
                new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

            if (!string.Equals(
                    connectionStringBuilder.InitialCatalog,
                    "BinanceAnalysis_IntegrationTests",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Security integration tests must use BinanceAnalysis_IntegrationTests.");
            }

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddJsonFile(
                    testSettingsPath,
                    optional: false,
                    reloadOnChange: false);

                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = connectionString
                    });
            });

            builder.ConfigureTestServices(services =>
            {
                // Do not connect to Binance during API integration tests.
                var ingestionService = services.FirstOrDefault(
                    descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name == "TradeIngestionService");

                if (ingestionService is not null)
                {
                    services.Remove(ingestionService);
                }
            });
        }
    }
}
