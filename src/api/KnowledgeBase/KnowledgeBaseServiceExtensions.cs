using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Options;
using Intranet.Api.KnowledgeBase.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Intranet.Api.KnowledgeBase;

public static class KnowledgeBaseServiceExtensions
{
    public static IServiceCollection AddKnowledgeBase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KnowledgeBaseOptions>(configuration.GetSection(KnowledgeBaseOptions.SectionName));

        var kbSection = configuration.GetSection(KnowledgeBaseOptions.SectionName);
        var connectionString = kbSection["ConnectionString"]
            ?? configuration.GetConnectionString("KnowledgeDb")
            ?? "Host=localhost;Port=5433;Database=knowledge;Username=knowledge;Password=knowledge_dev_password";

        services.PostConfigure<KnowledgeBaseOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.ConnectionString))
            {
                o.ConnectionString = connectionString;
            }

            o.PythonPath = ResolvePythonPath(o);
        });

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(dataSource, o => o.UseVector()));

        services.Configure<WebSearchOptions>(configuration.GetSection(WebSearchOptions.SectionName));

        services.AddHttpClient<OllamaClient>();
        services.AddHttpClient<WebSearchService>();
        services.AddScoped<SemanticSearchService>();
        services.AddScoped<WebSearchService>();
        services.AddScoped<ChatSearchRouter>();
        services.AddScoped<ChatExportService>();
        services.AddScoped<RagService>();
        services.AddScoped<KnowledgeUploadStaging>();
        services.AddScoped<IngestService>();

        return services;
    }

    public static async Task InitializeKnowledgeDatabaseAsync(this WebApplication app)
    {
        var options = app.Configuration.GetSection(KnowledgeBaseOptions.SectionName).Get<KnowledgeBaseOptions>()
            ?? new KnowledgeBaseOptions();
        var connectionString = options.ConnectionString
            ?? app.Configuration.GetConnectionString("KnowledgeDb")
            ?? "Host=localhost;Port=5433;Database=knowledge;Username=knowledge;Password=knowledge_dev_password";

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("KnowledgeDbInit");

        try
        {
            var migrationDir = ResolveMigrationsDirectory(app, options);
            if (Directory.Exists(migrationDir))
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                foreach (var migrationPath in Directory.GetFiles(migrationDir, "*.sql").OrderBy(f => f))
                {
                    var sql = await File.ReadAllTextAsync(migrationPath);
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Knowledge database schema applied from {Path}", migrationPath);
                }
            }
            else
            {
                logger.LogWarning("Knowledge migrations directory not found at {Path}", migrationDir);
            }

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
            _ = await db.Database.CanConnectAsync();
            logger.LogInformation("Knowledge database connected.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Knowledge database initialization failed. KB endpoints may be unavailable.");
        }
    }

    private static string ResolveMigrationsDirectory(WebApplication app, KnowledgeBaseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.MigrationSqlPath))
        {
            var path = Path.IsPathRooted(options.MigrationSqlPath)
                ? options.MigrationSqlPath
                : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, options.MigrationSqlPath));
            if (Directory.Exists(path))
            {
                return path;
            }

            return File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        }

        var etcKgRelative = Path.Combine(app.Environment.ContentRootPath, "..", "..", "..", "etc-kg", "migrations");
        if (Directory.Exists(etcKgRelative))
        {
            return Path.GetFullPath(etcKgRelative);
        }

        return Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, options.EtcKgPath, "migrations"));
    }

    private static string ResolvePythonPath(KnowledgeBaseOptions options)
    {
        if (!string.Equals(options.PythonPath, "python", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.PythonPath, "py", StringComparison.OrdinalIgnoreCase))
        {
            return options.PythonPath;
        }

        var etcKgRoot = Path.IsPathRooted(options.EtcKgPath)
            ? options.EtcKgPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.EtcKgPath));

        var venvPython = Path.Combine(etcKgRoot, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        venvPython = Path.Combine(etcKgRoot, ".venv", "bin", "python");
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        return OperatingSystem.IsWindows() ? "py" : "python3";
    }
}
