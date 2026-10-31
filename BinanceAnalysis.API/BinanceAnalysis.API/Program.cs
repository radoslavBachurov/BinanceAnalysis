
namespace BinanceAnalysis.API
{
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.DependencyInjection;
    using BinanceAnalysis.Data.Seeding;
    using Microsoft.EntityFrameworkCore;
    using BinanceAnalysis.Services.Data.DependencyInjection;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.

            builder.Services
            .AddControllers()
            .AddXmlSerializerFormatters();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddDataServices(builder.Configuration);
            builder.Services.AddTradeIngestion(builder.Configuration);

            var app = builder.Build();

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                await dbContext.Database.MigrateAsync();

                var seeder = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContextSeeder>();

                await seeder.SeedAsync();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
