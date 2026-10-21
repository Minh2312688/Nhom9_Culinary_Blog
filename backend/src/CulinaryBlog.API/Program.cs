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
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
var allowedOrigins = CulinaryBlog.Application.Common.Security.CorsPolicyValidator.ValidateAndFilterAllowedOrigins(
    configuredOrigins,
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));

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

// SRS NFR-SEC-006: Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CulinaryBlog.Domain.Constants.AppPolicies.AdminPolicy, policy =>
        policy.RequireRole(CulinaryBlog.Domain.Constants.AppRoles.Admin));
    options.AddPolicy(CulinaryBlog.Domain.Constants.AppPolicies.AuthorPolicy, policy =>
        policy.RequireRole(CulinaryBlog.Domain.Constants.AppRoles.Author, CulinaryBlog.Domain.Constants.AppRoles.Admin));
});


// NFR-SEC-003: Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, seconds).ToString();
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
            status = StatusCodes.Status429TooManyRequests,
            detail = "Rate limit exceeded. Please try again later."
        }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: token);
    };

    // 1. General API: 100 requests / minute / IP (Fixed Window)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        if ((builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")) &&
            httpContext.Request.Headers.TryGetValue("X-Test-Client-IP", out var testIp))
        {
            ipAddress = testIp.ToString();
        }

        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // 2. Auth Endpoints: 10 requests / minute / IP (Sliding Window, 6 segments)
    options.AddPolicy("AuthRateLimitPolicy", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return RateLimitPartition.GetSlidingWindowLimiter(ipAddress, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0
        });
    });

    // 3. Upload Endpoints: 5 requests / minute / IP (Fixed Window)
    options.AddPolicy("UploadRateLimitPolicy", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
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
app.MapCategoryEndpoints();
app.MapRecipeEndpoints();
app.MapRecipeImageEndpoints();
app.MapAuthorEndpoints();
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
