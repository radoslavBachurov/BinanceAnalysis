
namespace BinanceAnalysis.Data.Seeding
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;

    public class MonitoredSymbolsSeeder : ISeeder
    {
        private readonly ApplicationDbContext context;

        public MonitoredSymbolsSeeder(ApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            string[] symbols = ["BTCUSDT", "ADAUSDT", "ETHUSDT"];

            foreach (var symbol in symbols)
            {
                var exists = await this.context.MonitoredSymbols
                    .AnyAsync(x => x.Symbol == symbol, cancellationToken);

                if (!exists)
                {
                    await this.context.MonitoredSymbols.AddAsync(
                        new MonitoredSymbol
                        {
                            Symbol = symbol,
                            IsActive = true,
                            CreatedAtUtc = DateTime.UtcNow
                        },
                        cancellationToken);
                }
            }

            await this.context.SaveChangesAsync(cancellationToken);
        }
    }
}
