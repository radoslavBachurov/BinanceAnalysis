
namespace BinanceAnalysis.Data.Seeding
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class ApplicationDbContextSeeder
    {
        private readonly IEnumerable<ISeeder> seeders;
        private readonly ILogger<ApplicationDbContextSeeder> logger;

        public ApplicationDbContextSeeder(
            IEnumerable<ISeeder> seeders,
            ILogger<ApplicationDbContextSeeder> logger)
        {
            this.seeders = seeders;
            this.logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            foreach (var seeder in this.seeders)
            {
                await seeder.SeedAsync(cancellationToken);

                this.logger.LogInformation(
                    "Seeder {SeederName} completed.",
                    seeder.GetType().Name);
            }
        }
    }
}
