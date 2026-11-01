namespace BinanceAnalysis.Tests.Integration
{
    using BinanceAnalysis.Data;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;

    public sealed class SqlServerIntegrationTestFixture
    {
        private const string ExpectedDatabaseName =
            "BinanceAnalysis_IntegrationTests";

        private readonly string connectionString;

        public SqlServerIntegrationTestFixture()
        {
            var configuration = new ConfigurationBuilder()
       .SetBasePath(AppContext.BaseDirectory)
       .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
       .Build();

            this.connectionString =
                configuration.GetConnectionString(
                    "IntegrationTestConnection")
                ?? throw new InvalidOperationException(
                    "IntegrationTestConnection is not configured.");

            var connectionStringBuilder =
                new SqlConnectionStringBuilder(this.connectionString);

            if (!string.Equals(
                connectionStringBuilder.InitialCatalog,
                ExpectedDatabaseName,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Integration tests must use the '{ExpectedDatabaseName}' database. " +
                    $"Configured database: '{connectionStringBuilder.InitialCatalog}'.");
            }

            using var context = this.CreateDbContext();

            context.Database.Migrate();
        }

        public ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(this.connectionString)
                .Options;

            return new ApplicationDbContext(options);
        }

        public ApplicationDbContext CreateDbContext(
            IInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(this.connectionString)
                .AddInterceptors(interceptor)
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}