namespace BinanceAnalysis.Data.Seeding
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISeeder
    {
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}