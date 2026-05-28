using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

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
});

app.MapGet("/api/messages", async (IntranetDbContext db, CancellationToken cancellationToken) =>
{
    var messages = await db.SiteMessages
        .OrderByDescending(m => m.CreatedAt)
        .Take(10)
        .Select(m => new { m.Id, m.Title, m.Body, m.CreatedAt })
        .ToListAsync(cancellationToken);

    return Results.Ok(messages);
});

app.MapFallbackToFile("index.html");

app.Run();
