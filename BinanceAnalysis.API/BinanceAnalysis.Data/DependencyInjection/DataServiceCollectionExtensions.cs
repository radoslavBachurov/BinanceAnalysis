
namespace BinanceAnalysis.Data.DependencyInjection
{
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Seeding;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public static class DataServiceCollectionExtensions
    {
        public static IServiceCollection AddDataServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not found.");

            services.AddDbContext<ApplicationDbContext>(options =>
         options.UseSqlServer(
         connectionString,
         sqlServerOptions =>
         {
             sqlServerOptions.EnableRetryOnFailure(
                 maxRetryCount: 3,
                 maxRetryDelay: TimeSpan.FromSeconds(5),
                 errorNumbersToAdd: null);
         }));

            services.AddScoped<ISeeder, MonitoredSymbolsSeeder>();
            services.AddScoped<ApplicationDbContextSeeder>();

            return services;
        }
    }
}
