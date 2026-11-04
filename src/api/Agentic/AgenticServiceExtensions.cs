using Intranet.Api.Agentic.Day;
using Intranet.Api.Agentic.Realtime;
using Intranet.Api.Agentic.Repository;
using Microsoft.Extensions.Options;

namespace Intranet.Api.Agentic;

public static class AgenticServiceExtensions
{
    public static IServiceCollection AddAgenticWorkspace(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgenticOptions>(configuration.GetSection(AgenticOptions.SectionName));
        services.AddScoped<LocalRepositorySearch>();
        services.AddScoped<IRepositorySearch>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AgenticOptions>>().Value;
            var local = sp.GetRequiredService<LocalRepositorySearch>();
            return string.Equals(options.SearchProvider, "azure", StringComparison.OrdinalIgnoreCase)
                ? new AzureAiSearchRepositorySearch(sp.GetRequiredService<IOptions<AgenticOptions>>(), local)
                : local;
        });
        services.AddSingleton<IDayDataSource, MockDayDataSource>();
        services.AddSingleton<IDayDataSource, GraphCalendarDayDataSource>();
        services.AddSingleton<IDayAggregator, DayAggregator>();
        services.AddScoped<IWorkspaceDirectory, WorkspaceDirectory>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<WorkspaceSeeder>();
        services.AddSignalR();
        return services;
    }

    public static IApplicationBuilder UseAgenticDevIdentity(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<AgenticOptions>>().Value;
            var path = context.Request.Path;
            var inWorkspace = path.StartsWithSegments("/api/workspace") || path.StartsWithSegments("/hubs/workspace");
            if (options.Enabled
                && options.AllowAnonymous
                && inWorkspace
                && context.User.Identity?.IsAuthenticated != true)
            {
                var claims = new[]
                {
                    new System.Security.Claims.Claim("oid", options.DemoUserOid),
                    new System.Security.Claims.Claim("name", options.DemoUserName),
                    new System.Security.Claims.Claim("email", options.DemoUserEmail),
                };
                context.User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(claims, "AgenticDev"));
            }

            await next();
        });
    }

    public static async Task SeedAgenticWorkspaceAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var options = app.Services.GetRequiredService<IOptions<AgenticOptions>>().Value;
        if (!options.Enabled || !options.SeedDemoData)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<WorkspaceSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    public static IEndpointRouteBuilder MapAgenticWorkspace(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<WorkspaceHub>("/hubs/workspace");
        return endpoints;
    }
}
