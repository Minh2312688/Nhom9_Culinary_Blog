using System.Text;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Infrastructure.Authentication;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Notifications;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment? environment = null)
    {
        // Database Context configuration (PostgreSQL strictly required by SRS)
        var postgresConnection = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(postgresConnection))
        {
            throw new InvalidOperationException("Connection string 'Postgres' is required for AuthDbContext.");
        }

        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(postgresConnection));

        // ASP.NET Core Identity configuration
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // NFR-SEC-001: Password policy
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;

            // FR-AUTH-002: Lockout policy (5 failed attempts -> 15 minutes lockout)
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // Email uniqueness
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AuthDbContext>()
        .AddDefaultTokenProviders();

        // NFR-SEC-001 Safety Patch: PBKDF2-HMACSHA512 iteration count >= 100,000
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = 100000;
        });

        // JWT Authentication configuration (CONS-004 / NFR-SEC-002: HS256, 15m TTL)
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("Configuration 'Jwt:Key' is required and cannot be empty.");
        }

        var jwtIssuer = configuration["Jwt:Issuer"] ?? "CulinaryBlog";
        var jwtAudience = configuration["Jwt:Audience"] ?? "CulinaryBlogApp";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // SRS NFR-SEC-007: Development/Test may disable HTTPS metadata; Production must strictly require it.
            options.RequireHttpsMetadata = environment != null &&
                !string.Equals(environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        // Core Auth Services
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenGenerator, JwtService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IWelcomeEmailEnqueuer, WelcomeEmailEnqueuer>();

        return services;
    }
}
