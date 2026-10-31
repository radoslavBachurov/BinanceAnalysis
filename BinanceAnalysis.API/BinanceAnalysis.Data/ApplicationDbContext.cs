namespace BinanceAnalysis.Data
{

    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();

        public DbSet<MonitoredSymbol> MonitoredSymbols => Set<MonitoredSymbol>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ApplicationDbContext).Assembly);
        }
    }
}
