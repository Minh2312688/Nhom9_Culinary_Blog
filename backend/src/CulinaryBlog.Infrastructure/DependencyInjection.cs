using System.Text;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Notifications;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Authentication;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Notifications;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using CulinaryBlog.Infrastructure.Storage;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Minio;

namespace CulinaryBlog.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment? environment = null)
    {
        // One PostgreSQL context owns Identity, auth tokens, recipes, and categories.
        var postgresConnection = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(postgresConnection))
        {
            throw new InvalidOperationException("Connection string 'Postgres' is required for ApplicationDbContext.");
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddScoped<IApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryCache, DistributedCategoryCache>();
        services.AddScoped<IRecipeCache, DistributedRecipeCache>();

        var redisConnection = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "CulinaryBlog:";
            });
        }

        var minioEndpoint = configuration["MinIO:Endpoint"];
        var minioAccessKey = configuration["MinIO:AccessKey"];
        var minioSecretKey = configuration["MinIO:SecretKey"];
        var minioUseSsl = bool.TryParse(configuration["MinIO:UseSSL"], out var parsedUseSsl) && parsedUseSsl;
        var bucketName = configuration["MinIO:BucketName"] ?? "culinary-blog";
        services.AddSingleton<IFileValidationService, FileValidationService>();

        if (string.IsNullOrWhiteSpace(minioEndpoint) ||
            string.IsNullOrWhiteSpace(minioAccessKey) ||
            string.IsNullOrWhiteSpace(minioSecretKey))
        {
            services.AddScoped<IFileStorageService, UnavailableFileStorageService>();
        }
        else
        {
            var publicBaseUrl = configuration["MinIO:PublicBaseUrl"] ??
                $"{(minioUseSsl ? "https" : "http")}://{minioEndpoint}/{bucketName}";

            services.AddSingleton<IMinioClient>(_ =>
            {
                var client = new MinioClient()
                    .WithEndpoint(minioEndpoint)
                    .WithCredentials(minioAccessKey, minioSecretKey);
                if (minioUseSsl) client = client.WithSSL();
                return client.Build();
            });
            services.AddSingleton<IObjectStorageClient, MinioObjectStorageClient>();
            services.AddSingleton<IHostedService>(serviceProvider => new StorageStartupInitializer(
                serviceProvider.GetRequiredService<IObjectStorageClient>(),
                serviceProvider.GetRequiredService<ILogger<StorageStartupInitializer>>(),
                bucketName));
            services.AddScoped<IFileStorageService>(provider => new MinioFileStorageService(
                provider.GetRequiredService<IObjectStorageClient>(),
                provider.GetRequiredService<IFileValidationService>(),
                bucketName,
                publicBaseUrl));
        }

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
        .AddEntityFrameworkStores<ApplicationDbContext>()
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

        // FR-JOB-001 (Welcome Email via Hangfire + MailKit/SMTP).
        // Skipped in the "Testing" environment so integration tests can run
        // without a real Hangfire storage or SMTP server; tests override
        // IWelcomeEmailEnqueuer with a spy instead.
        var isTestingEnvironment = string.Equals(
            environment?.EnvironmentName,
            "Testing",
            StringComparison.OrdinalIgnoreCase);

        if (!isTestingEnvironment)
        {
            // SMTP options: bound from "Smtp" section (appsettings/environment).
            // SmtpEmailSender validates at send time; empty section is allowed
            // here so the app can boot before SMTP is configured, but sending
            // will throw a clear error instead of fake success.
            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName));

            services.AddScoped<IEmailSender, SmtpEmailSender>();
            services.AddScoped<WelcomeEmailJob>();

            // Hangfire with PostgreSQL storage (persistent, per SRS traceability).
            // Reuses the existing Postgres connection string. The storage package
            // creates/upgrades the "hangfire" schema itself
            // (PrepareSchemaIfNecessary), so no manual schema or migration.
            services.AddHangfire((provider, config) =>
            {
                config.UsePostgreSqlStorage(
                    bootstrapper => bootstrapper.UseNpgsqlConnection(postgresConnection),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire",
                        PrepareSchemaIfNecessary = true,
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        // Fail fast if PostgreSQL is unreachable at startup:
                        // no resilient retry loop, no degraded mode. A clear
                        // exception beats a silently non-functional job server.
                        StartupConnectionMaxRetries = 0,
                        AllowDegradedModeWithoutStorage = false
                    });

                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                config.UseFilter(new WelcomeEmailFailureLogFilter(
                    loggerFactory.CreateLogger<WelcomeEmailJob>()));
            });

            // Background job server (no Dashboard in this scope).
            services.AddHangfireServer();

            services.AddScoped<IWelcomeEmailEnqueuer, WelcomeEmailEnqueuer>();
        }
        else
        {
            // Testing: no Hangfire storage/server (and therefore no
            // IBackgroundJobClient). Register a no-op enqueuer so the API can
            // boot and resolve the graph; integration tests that assert
            // enqueue behavior override IWelcomeEmailEnqueuer with a spy.
            services.AddScoped<IWelcomeEmailEnqueuer, NoOpWelcomeEmailEnqueuer>();
        }

        return services;
    }
}
