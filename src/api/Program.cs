using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IntranetDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IntranetDb")
        ?? throw new InvalidOperationException("Connection string 'IntranetDb' is not configured.");
    options.UseNpgsql(connectionString);
});

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IntranetDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IntranetDbContext>();
    await db.Database.MigrateAsync();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

app.MapGet("/api/me", [Authorize] (ClaimsPrincipal user) =>
{
    string? GetClaim(string name) => user.FindFirstValue(name);

    return Results.Ok(new
    {
        name = user.Identity?.Name ?? GetClaim("name"),
        email = GetClaim("preferred_username") ?? GetClaim("upn"),
        objectId = GetClaim("oid"),
        tenantId = GetClaim("tid"),
    });
});

app.MapFallbackToFile("index.html");

app.Run();
