using Intranet.Api.Cleat;
using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Intranet.Api.FeatureRequests;
using Intranet.Api.Help;
using Intranet.Api.KnowledgeBase;
using Intranet.Api.MultifamilyLbp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IntranetDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IntranetDb")
        ?? throw new InvalidOperationException("Connection string 'IntranetDb' is not configured.");
    options.UseNpgsql(connectionString);
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<IntranetDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();
builder.Services.Configure<CleatOptions>(builder.Configuration.GetSection(CleatOptions.SectionName));
builder.Services.AddHttpClient<CleatClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<CleatOptions>>().Value;
    var origin = options.ResolvedBaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri(origin + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<PipelineService>();
builder.Services.AddScoped<IFeatureRequestLlm, OllamaFeatureRequestLlm>();
builder.Services.AddScoped<FeatureRequestService>();
builder.Services.AddMultifamilyLbp(builder.Configuration);
builder.Services.AddKnowledgeBase(builder.Configuration);
builder.Services.AddHelpAgent();

var enableSwagger = builder.Environment.IsDevelopment()
    || builder.Configuration.GetValue("Swagger:Enabled", false);
if (enableSwagger)
{
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ETC Intranet API",
            Version = "v1",
            Description = "Intranet endpoints, CLEATUS opportunities/pipeline, and multifamily lead inspection.",
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Microsoft Entra access token with the API scope (same token the React app sends).",
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
    });
}
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
if (app.Environment.IsDevelopment())
{
    startupLogger.LogInformation(
        "Environment={Environment} AzureAd:TenantId={TenantId} AzureAd:Audience={Audience}",
        app.Environment.EnvironmentName,
        builder.Configuration["AzureAd:TenantId"],
        builder.Configuration["AzureAd:Audience"]);
}

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IntranetDbContext>();
    await db.Database.MigrateAsync();
    await app.MigrateMultifamilyDatabaseAsync();
    await app.InitializeKnowledgeDatabaseAsync();

    if (!await db.SiteMessages.AnyAsync())
    {
        db.SiteMessages.Add(new()
        {
            Title = "Welcome to ETC intranet",
            Body = "Your ETC React + .NET API + PostgreSQL starter is running.",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
catch (Exception ex)
{
    // Do not block Kestrel from starting — Azure health checks and logs need the process up.
    startupLogger.LogError(ex, "Database migration/seed failed at startup. Fix ConnectionStrings:IntranetDb in App Service settings.");
}

var sharePointSiteUrl = builder.Configuration["SharePoint:SiteUrl"];
var graphClientSecret = builder.Configuration["AzureAd:ClientSecret"];
if (string.IsNullOrWhiteSpace(sharePointSiteUrl) || string.IsNullOrWhiteSpace(graphClientSecret))
{
    startupLogger.LogWarning(
        "SharePoint import is disabled. Set SharePoint:SiteUrl and AzureAd:ClientSecret (plus Graph Sites.Read.All application permission) to enable import-legacy.");
}

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ETC Intranet API v1");
        options.DocumentTitle = "ETC Intranet API";
        options.RoutePrefix = "swagger";
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Liveness: no DB — used by Azure App Service health check (see app-service.bicep).
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", timestamp = DateTimeOffset.UtcNow }));

app.MapHealthChecks("/health");

app.MapGet("/api/status", async (IntranetDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    var messageCount = canConnect ? await db.SiteMessages.CountAsync(cancellationToken) : 0;

    return Results.Ok(new
    {
        service = "intranet-api",
        database = canConnect ? "connected" : "unavailable",
        messageCount,
        timestamp = DateTimeOffset.UtcNow,
    });
}).RequireAuthorization();

app.MapGet("/api/messages", async (IntranetDbContext db, CancellationToken cancellationToken) =>
{
    var messages = await db.SiteMessages
        .OrderByDescending(m => m.CreatedAt)
        .Take(10)
        .Select(m => new { m.Id, m.Title, m.Body, m.CreatedAt })
        .ToListAsync(cancellationToken);

    return Results.Ok(messages);
}).RequireAuthorization();

app.MapControllers().RequireAuthorization();

app.MapGet("/api/cleat/recommendations", async (
    CleatClient cleat,
    CancellationToken cancellationToken,
    double? minScore,
    int? limit) =>
{
    try
    {
        var score = minScore is null ? 80 : Math.Clamp(minScore.Value, 0, 100);
        var pageSize = limit is null ? 20 : Math.Clamp(limit.Value, 1, 100);
        var result = await cleat.GetRecommendationsAsync(score, pageSize, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is CleatNotConfiguredException or CleatUpstreamException)
    {
        return MapCleatError(ex);
    }
}).RequireAuthorization();

app.MapGet("/api/cleat/opportunities/{id}", async (
    string id,
    CleatClient cleat,
    CancellationToken cancellationToken) =>
{
    if (!CleatClient.IsValidOpportunityId(id))
    {
        return Results.Json(
            new CleatErrorResponse
            {
                Error = "invalid_opportunity_id",
                Message = "Opportunity id looks invalid.",
            },
            statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        var opportunity = await cleat.GetOpportunityAsync(id, cancellationToken);
        return opportunity is null
            ? Results.Json(
                new CleatErrorResponse
                {
                    Error = "cleat_not_found",
                    Message = "Opportunity not found in CLEATUS.",
                },
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(opportunity);
    }
    catch (Exception ex) when (ex is CleatNotConfiguredException or CleatUpstreamException)
    {
        return MapCleatError(ex);
    }
}).RequireAuthorization();

app.MapGet("/api/cleat/pipeline", async (
    PipelineService pipeline,
    CancellationToken cancellationToken) =>
{
    try
    {
        var dashboard = await pipeline.GetDashboardAsync(cancellationToken);
        return Results.Ok(dashboard);
    }
    catch (Exception ex) when (ex is CleatNotConfiguredException or CleatUpstreamException)
    {
        return MapCleatError(ex);
    }
}).RequireAuthorization();

app.MapPost("/api/cleat/pursuits/{id}/close-out", async (
    string id,
    CloseoutRequest request,
    PipelineService pipeline,
    CancellationToken cancellationToken) =>
{
    if (!CleatClient.IsValidOpportunityId(id))
    {
        return Results.Json(
            new CleatErrorResponse
            {
                Error = "invalid_pursuit_id",
                Message = "Pursuit id looks invalid.",
            },
            statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
        var result = await pipeline.CloseOutAsync(id, request, cancellationToken);
        if (result.CleatusUpdated)
        {
            return Results.Ok(result);
        }

        var status = result.Error == "cleat_api_key_missing"
            ? StatusCodes.Status503ServiceUnavailable
            : result.Error == "cleat_not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status502BadGateway;
        return Results.Json(result, statusCode: status);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(
            new CleatErrorResponse { Error = "invalid_closeout", Message = ex.Message },
            statusCode: StatusCodes.Status400BadRequest);
    }
    catch (Exception ex) when (ex is CleatNotConfiguredException or CleatUpstreamException)
    {
        return MapCleatError(ex);
    }
}).RequireAuthorization();

app.MapPost("/api/feature-requests", async (
    CreateFeatureRequestBody body,
    FeatureRequestService features,
    ClaimsPrincipal user,
    CancellationToken cancellationToken) =>
{
    try
    {
        var created = await features.CreateAsync(
            body.Page ?? string.Empty,
            body.RawText ?? string.Empty,
            CreatedByFromUser(user),
            cancellationToken);
        return Results.Ok(created);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(
            new { error = "invalid_feature_request", message = ex.Message },
            statusCode: StatusCodes.Status400BadRequest);
    }
}).RequireAuthorization();

app.MapGet("/api/feature-requests", async (
    FeatureRequestService features,
    CancellationToken cancellationToken) =>
{
    var items = await features.ListAsync(cancellationToken);
    return Results.Ok(new { items });
}).RequireAuthorization();

app.MapPatch("/api/feature-requests/{id:int}", async (
    int id,
    UpdateFeatureRequestStatusBody body,
    FeatureRequestService features,
    CancellationToken cancellationToken) =>
{
    try
    {
        var updated = await features.UpdateStatusAsync(id, body.Status ?? string.Empty, cancellationToken);
        return updated is null
            ? Results.Json(
                new { error = "not_found", message = "Feature request not found." },
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(
            new { error = "invalid_status", message = ex.Message },
            statusCode: StatusCodes.Status400BadRequest);
    }
}).RequireAuthorization();

app.MapGet("/api/me", [Authorize] (ClaimsPrincipal user) =>
{
    static string? FirstClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    var identityName = user.Identity?.Name;
    var nameClaim = FirstClaim(user, "name", ClaimTypes.Name);
    var email = FirstClaim(
        user,
        "preferred_username",
        "email",
        ClaimTypes.Email,
        "upn",
        ClaimTypes.Upn,
        "unique_name");
    var objectId = FirstClaim(
        user,
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        ClaimTypes.NameIdentifier,
        "sub");
    var tenantId = FirstClaim(
        user,
        "tid",
        "http://schemas.microsoft.com/identity/claims/tenantid",
        "tenant_id");

    // Identity.Name is often UPN/email in Entra; prefer a real display name when available.
    var displayName = nameClaim;
    if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(identityName) && !identityName.Contains('@'))
    {
        displayName = identityName;
    }

    email ??= identityName?.Contains('@', StringComparison.Ordinal) == true ? identityName : null;

    return Results.Ok(new
    {
        name = displayName,
        email,
        objectId,
        tenantId,
    });
});

app.MapFallbackToFile("index.html");

app.Run();

static string CreatedByFromUser(ClaimsPrincipal user)
{
    static string? First(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    var email = First(
        user,
        "preferred_username",
        "email",
        ClaimTypes.Email,
        "upn",
        ClaimTypes.Upn,
        "unique_name");
    if (string.IsNullOrWhiteSpace(email) && user.Identity?.Name?.Contains('@', StringComparison.Ordinal) == true)
    {
        email = user.Identity.Name;
    }

    var objectId = First(
        user,
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        ClaimTypes.NameIdentifier,
        "sub");

    return email ?? objectId ?? "unknown";
}

static IResult MapCleatError(Exception ex) => ex switch
{
    CleatNotConfiguredException => Results.Json(
        new CleatErrorResponse
        {
            Error = "cleat_api_key_missing",
            Message = CleatNotConfiguredException.UserMessage,
        },
        statusCode: StatusCodes.Status503ServiceUnavailable),
    CleatUpstreamException { StatusCode: 404 } upstream => Results.Json(
        new CleatErrorResponse { Error = upstream.ErrorCode, Message = upstream.Message },
        statusCode: StatusCodes.Status404NotFound),
    CleatUpstreamException { StatusCode: 503 or 504 } upstream => Results.Json(
        new CleatErrorResponse { Error = upstream.ErrorCode, Message = upstream.Message },
        statusCode: upstream.StatusCode),
    CleatUpstreamException upstream => Results.Json(
        new CleatErrorResponse { Error = upstream.ErrorCode, Message = upstream.Message },
        statusCode: StatusCodes.Status502BadGateway),
    _ => Results.Json(
        new CleatErrorResponse
        {
            Error = "cleat_upstream_error",
            Message = "CLEATUS request failed.",
        },
        statusCode: StatusCodes.Status502BadGateway),
};
