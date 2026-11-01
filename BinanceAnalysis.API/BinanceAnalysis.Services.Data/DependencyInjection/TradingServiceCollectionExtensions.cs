
namespace BinanceAnalysis.Services.Data.DependencyInjection
{
    using BinanceAnalysis.API.Infrastructure.Binance;
    using BinanceAnalysis.Services.Data.Symbols;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using BinanceAnalysis.API.Infrastructure.Binance.Validation;

    public static class TradingServiceCollectionExtensions
    {
        public static IServiceCollection AddTradeIngestion(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<TradeIngestionOptions>(
                configuration.GetSection(
                    TradeIngestionOptions.SectionName));

            services.AddSingleton<BinanceWebSocketClient>();

            services.AddSingleton<IBinanceWebSocketClient>(
                provider => provider.GetRequiredService<BinanceWebSocketClient>());

            services.AddScoped<TradePersistenceService>();

            services.AddSingleton<TimeProvider>(TimeProvider.System);

            services.AddHostedService<TradeIngestionService>();

            services.AddScoped<IPriceAnalysisService, PriceAnalysisService>();

            services.AddScoped<IMonitoredSymbolService, MonitoredSymbolService>();

            services.AddHttpClient<IBinanceSymbolValidationClient, BinanceSymbolValidationClient>(
            client =>
            {
            client.BaseAddress = new Uri("https://api.binance.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            });

            return services;
        }
    }
}
