
namespace BinanceAnalysis.API
{
    using BinanceAnalysis.API.Configuration;
    using BinanceAnalysis.API.Exceptions;
    using BinanceAnalysis.API.Services.Authentication;
    using BinanceAnalysis.API.Swagger;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.DependencyInjection;
    using BinanceAnalysis.Data.Seeding;
    using BinanceAnalysis.Services.Data.DependencyInjection;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi;
    using Serilog;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.

            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            });

            builder.Services
            .AddControllers()
            .AddXmlSerializerFormatters();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(options =>
            {
                var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                options.IncludeXmlComments(xmlPath);

                options.OperationFilter<SwaggerParameterExampleOperationFilter>();


                options.AddSecurityDefinition(
                    JwtBearerDefaults.AuthenticationScheme,
                    new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "Enter your JWT access token."
                    });

                options.AddSecurityRequirement(document =>
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(
                            JwtBearerDefaults.AuthenticationScheme,
                            document)] = []
                    });

            });


            builder.Services.AddDataServices(builder.Configuration);
            builder.Services.AddTradeIngestion(builder.Configuration);

            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            builder.Services.AddSingleton<
            IValidateOptions<JwtOptions>,
            JwtOptionsValidator>();

            builder.Services.AddSingleton<
                IValidateOptions<AdminCredentialsOptions>,
                AdminCredentialsOptionsValidator>();

            builder.Services
                .AddOptions<JwtOptions>()
                .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
                .ValidateOnStart();

            builder.Services
                .AddOptions<AdminCredentialsOptions>()
                .Bind(builder.Configuration.GetSection(
                    AdminCredentialsOptions.SectionName))
                .ValidateOnStart();

            builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

            builder.Services
                .AddOptions<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>(
                    (options, jwtOptionsAccessor) =>
                    {
                        var jwtOptions = jwtOptionsAccessor.Value;

                        options.MapInboundClaims = false;

                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = jwtOptions.Issuer,

                            ValidateAudience = true,
                            ValidAudience = jwtOptions.Audience,

                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Convert.FromBase64String(jwtOptions.SigningKey)),

                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30),

                            NameClaimType = "name",
                            RoleClaimType = "role"
                        };
                    });

            builder.Services.AddSingleton<
            IJwtTokenService,
            JwtTokenService>();

            if (builder.Environment.IsProduction())
            {
                builder.Services
            .AddOptions<MarketPriceRetentionOptions>()
            .Bind(builder.Configuration.GetSection(
                MarketPriceRetentionOptions.SectionName))
            .Validate(
                options => options.RetentionHours > 0,
                "RetentionHours must be greater than zero.")
            .Validate(
                options => options.CleanupIntervalMinutes > 0,
                "CleanupIntervalMinutes must be greater than zero.")
            .Validate(
                options => options.BatchSize > 0,
                "BatchSize must be greater than zero.")
            .ValidateOnStart();
            }

            var app = builder.Build();

            app.UseExceptionHandler();

            try
            {
                await using var scope = app.Services.CreateAsyncScope();

                var dbContext = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                await dbContext.Database.MigrateAsync();

                var seeder = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContextSeeder>();

                await seeder.SeedAsync();
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "Application startup failed.");
                throw;
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
