using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Intranet.Api.MultifamilyLbp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
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
builder.Services.AddMultifamilyLbp(builder.Configuration);
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ETC Intranet API",
        Version = "v1",
        Description = "Intranet endpoints and multifamily lead inspection (jobs, uploads, normalization, reports).",
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
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});
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
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IntranetDbContext>();
    await db.Database.MigrateAsync();
    await app.MigrateMultifamilyDatabaseAsync();

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

var swaggerEnabled = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Swagger:Enabled", false);

if (swaggerEnabled)
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
