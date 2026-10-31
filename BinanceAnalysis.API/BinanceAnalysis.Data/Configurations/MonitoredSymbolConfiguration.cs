namespace BinanceAnalysis.Data.Configurations
{
    using BinanceAnalysis.Data.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    public class MonitoredSymbolConfiguration : IEntityTypeConfiguration<MonitoredSymbol>
    {
        public void Configure(EntityTypeBuilder<MonitoredSymbol> builder)
        {
            builder.ToTable("MonitoredSymbols");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Symbol)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => x.Symbol)
                .IsUnique();
        }
    }
}
