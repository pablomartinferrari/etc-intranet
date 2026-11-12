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
        services.AddSingleton<IDayDataSource>(sp =>
            new MockDayDataSource(sp.GetRequiredService<IHostEnvironment>()));
        services.AddScoped<IUserGraphTokenProvider, ObOUserGraphTokenProvider>();
        services.AddScoped<IUserGraphCalendarClient, GraphUserCalendarClient>();
        services.AddScoped<IUserGraphMailClient, GraphUserMailClient>();
        services.AddScoped<IMeetingBriefGenerator, MeetingBriefGenerator>();
        services.AddScoped<IDayAggregator, DayAggregator>();
        services.AddScoped<IWorkspaceDirectory, WorkspaceDirectory>();
        services.AddScoped<IWorkspaceChatComposer, WorkspaceChatComposer>();
        services.AddScoped<IRepositoryReindexService, RepositoryReindexService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<WorkspaceSeeder>();
        services.AddScoped<WorkspaceDemoCleanup>();
        services.AddHostedService<RepositoryReindexWorker>();
        services.AddSignalR();
        return services;
    }

    public static IApplicationBuilder UseAgenticDevIdentity(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<AgenticOptions>>().Value;
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var path = context.Request.Path;
            var inWorkspace = path.StartsWithSegments("/api/workspace") || path.StartsWithSegments("/hubs/workspace");
            if (options.Enabled
                && options.AllowAnonymous
                && !environment.IsProduction()
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

    public static async Task InitializeAgenticWorkspaceAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var options = app.Services.GetRequiredService<IOptions<AgenticOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<WorkspaceDemoCleanup>();
        if (options.SeedDemoData && !app.Environment.IsProduction())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<WorkspaceSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        else
        {
            await cleanup.RunAsync(cancellationToken);
        }

        await cleanup.EnsureRootFolderAsync(cancellationToken);
    }

    public static IEndpointRouteBuilder MapAgenticWorkspace(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<WorkspaceHub>("/hubs/workspace");
        return endpoints;
    }
}
