namespace BinanceAnalysis.Data.Configurations
{
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    public class MarketPriceConfiguration : IEntityTypeConfiguration<MarketPrice>
    {
        public void Configure(EntityTypeBuilder<MarketPrice> builder)
        {
            builder.ToTable("MarketPrices");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Symbol)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.TradeId)
                .IsRequired();

            builder.Property(x => x.Price)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(x => x.ObservedAtUtc)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => new { x.Symbol, x.TradeId })
                .IsUnique();

            builder.HasIndex(x => new { x.Symbol, x.ObservedAtUtc });
        }
    }
}
