using Intranet.Api.Cleat;
using Intranet.Api.Data;
using Intranet.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IntranetDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IntranetDb")
        ?? throw new InvalidOperationException("Connection string 'IntranetDb' is not configured.");
    options.UseNpgsql(connectionString);
});

builder.Services.Configure<CleatOptions>(builder.Configuration.GetSection(CleatOptions.SectionName));
builder.Services.AddHttpClient<CleatClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<CleatOptions>>().Value;
    var origin = string.IsNullOrWhiteSpace(options.BaseUrl)
        ? "https://api.cleat.ai"
        : options.BaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri(origin + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
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
});

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
});

app.MapFallbackToFile("index.html");

app.Run();

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
