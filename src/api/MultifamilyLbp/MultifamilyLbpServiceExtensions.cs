using System.Text.Json;
using System.Text.Json.Serialization;
using Intranet.Api.MultifamilyLbp.Data;
using Intranet.Api.MultifamilyLbp.Options;
using Intranet.Api.MultifamilyLbp.Services;
using Intranet.Api.MultifamilyLbp.Services.Excel;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Api.MultifamilyLbp;

public static class MultifamilyLbpServiceExtensions
{
    public static IServiceCollection AddMultifamilyLbp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));
        services.Configure<SharePointOptions>(configuration.GetSection(SharePointOptions.SectionName));

        // Use IntranetDb first — Azure App Service sets ConnectionStrings__IntranetDb in production.
        // appsettings.json may still define MultifamilyDb for local dev; prefer IntranetDb so a
        // bundled localhost MultifamilyDb value cannot override the deployed connection string.
        var connectionString = configuration.GetConnectionString("IntranetDb")
            ?? configuration.GetConnectionString("MultifamilyDb")
            ?? throw new InvalidOperationException(
                "Connection string 'IntranetDb' or 'MultifamilyDb' must be configured.");

        services.AddDbContext<MultifamilyDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddHttpClient();
        services.AddSingleton<IParserClient, XrfWorkbookParser>();
        services.AddSingleton<MultifamilyReadingsService>();
        services.AddScoped<JobEntityService>();
        services.AddScoped<EntityDashboardService>();
        services.AddScoped<EntityUploadService>();
        services.AddScoped<EntityRowsService>();
        services.AddScoped<NormalizationService>();
        services.AddScoped<ReportGenerationService>();
        services.AddScoped<ReportExcelExportService>();

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        return services;
    }

    public static async Task MigrateMultifamilyDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultifamilyDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MultifamilyDbInit");

        try
        {
            if (db.Database.GetMigrations().Any())
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Multifamily database migrated.");
                return;
            }

            logger.LogWarning(
                "No EF migrations registered for MultifamilyDbContext; creating schema with EnsureCreated.");
            await db.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Multifamily database initialization failed.");
            try
            {
                await db.Database.EnsureCreatedAsync();
                logger.LogWarning("Multifamily schema created via EnsureCreated after migration failure.");
            }
            catch (Exception ensureEx)
            {
                logger.LogError(ensureEx, "EnsureCreated also failed for MultifamilyDbContext.");
                throw;
            }
        }
    }
}
