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

        var connectionString = configuration.GetConnectionString("MultifamilyDb")
            ?? configuration.GetConnectionString("IntranetDb")
            ?? throw new InvalidOperationException(
                "Connection string 'MultifamilyDb' or 'IntranetDb' must be configured.");

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
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Multifamily migration failed; ensuring database is created.");
            await db.Database.EnsureCreatedAsync();
        }
    }
}
