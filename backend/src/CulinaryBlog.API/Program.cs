using System.Threading.RateLimiting;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Notifications;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// Đăng ký các service của các layer
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CulinaryBlog.API.CurrentUserService>();

// CORS for Frontend (Next.js) - SRS NFR-SEC-005: Configured origins only, never wildcard.
// Development allows local defaults (3000/3001); Non-Development strictly requires configured origins.
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
var allowedOrigins = configuredOrigins ??
    (builder.Environment.IsDevelopment()
        ? new[] { "http://localhost:3000", "http://localhost:3001" }
        : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// NFR-SEC-003: Rate Limiting for /auth/* (10 requests / minute / IP with Sliding Window)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }
        else
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = 429,
            detail = "Rate limit exceeded. Please try again later."
        }, cancellationToken: token);
    };

    // Security (NFR-SEC-003): Do not trust raw X-Forwarded-For from untrusted clients.
    // Use connection's resolved RemoteIpAddress.
    // TEST-ONLY TECHNICAL DEBT: In Development/Testing only, allow isolated test runs via X-Test-Client-IP.
    // This cannot execute in Staging/Production.
    options.AddPolicy("AuthRateLimitPolicy", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        if ((builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")) &&
            httpContext.Request.Headers.TryGetValue("X-Test-Client-IP", out var testIp))
        {
            ipAddress = testIp.ToString();
        }

        return RateLimitPartition.GetSlidingWindowLimiter(ipAddress, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "CulinaryBlog.API",
    phase = "TV1 - Giai doan 2 Auth",
    status = "running"
}));
app.UseAuthentication();
app.UseAuthorization();
// Đăng ký tất cả endpoints
app.MapCategoryEndpoints();
app.MapRecipeEndpoints();
app.MapRecipeImageEndpoints();
app.MapSystemEndpoints();
app.MapAuthEndpoints();

// Apply the unified PostgreSQL schema and optionally seed local sample data.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CulinaryBlog.Infrastructure.Identity.ApplicationUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
    {
        await db.Database.MigrateAsync();
        if (app.Configuration.GetValue("Seed:Enabled", true))
        {
            await CulinaryBlog.Infrastructure.Persistence.Seed.Lab02DataSeeder.SeedAsync(db, userManager, logger);
        }
    }
}

// FR-JOB-001 / SRS-AUDIT sec-12-5: Hangfire Dashboard at /hangfire, protected
// by the Admin-only Bearer filter (HangfireDashboardAuthorizationFilter).
// Registered only when Hangfire is configured (not "Testing", which skips
// Hangfire storage/server entirely). See HangfireDashboardAuthorizationFilter
// for the bearer-only policy: no query-string token, no cookie, no login
// flow; the Dashboard UI is not considered usable by Admin until the team
// confirms a secure provisioning flow.
if (!app.Environment.IsEnvironment("Testing"))
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter(app.Configuration)]
    });
}

app.Run();
