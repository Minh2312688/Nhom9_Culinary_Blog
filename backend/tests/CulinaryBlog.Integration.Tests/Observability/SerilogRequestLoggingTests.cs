using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

[Collection("SerilogSinks")]
public class SerilogRequestLoggingTests : IDisposable
{
    private const string HealthPath = "/health/live";
    private const string CategoriesPath = "/api/v1/categories";
    private const string AuthLoginPath = "/api/v1/auth/login";
    private const string SentinelToken = "SENTINEL-TOKEN-123";
    private const string SentinelPassword = "SENTINEL-PASSWORD-123";
    private const string SentinelConfig = "SENTINEL-CONFIG-123";

    public void Dispose()
    {
    }

    [Fact]
    public async Task RequestLogging_Authenticated_ShouldIncludeHttpMethodAndUserId()
    {
        // Arrange: Create a fresh factory with a collecting sink
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();

        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:MinimumLevel"] = "Debug"
                });
            });
        });

        using var client = CreateClientAsAdmin(host);

        // Act
        var response = await client.GetAsync(HealthPath);

        // Assert
        response.EnsureSuccessStatusCode();
        var logEvent = await FindRequestSummaryEventAsync(factory, HealthPath);
        Assert.NotNull(logEvent);
        Assert.Equal("GET", GetScalarValue(logEvent, "HttpMethod"));
        Assert.Equal(HealthPath, GetScalarValue(logEvent, "RequestPath"));
        Assert.Equal("test-admin", GetScalarValue(logEvent, "UserId"));
    }

    [Fact]
    public async Task RequestLogging_Anonymous_ShouldNotIncludeUserId()
    {
        // Arrange
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:MinimumLevel"] = "Debug"
                });
            });
        });
        using var client = host.CreateClient();

        // Act
        var response = await client.GetAsync(HealthPath);

        // Assert
        response.EnsureSuccessStatusCode();
        var logEvent = await FindRequestSummaryEventAsync(factory, HealthPath);
        Assert.NotNull(logEvent);
        Assert.Equal("GET", GetScalarValue(logEvent, "HttpMethod"));
        Assert.Equal(HealthPath, GetScalarValue(logEvent, "RequestPath"));
        Assert.False(logEvent.Properties.ContainsKey("UserId"));
    }

    [Fact]
    public async Task RequestLogging_HealthEndpoint_Success_LogsAtDebugLevel()
    {
        // Arrange: /health/live succeeds (200) and has no exceptions.
        // It logs at Debug only when Serilog:MinimumLevel=Debug is configured for this host.
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();

        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:MinimumLevel"] = "Debug"
                });
            });
        });

        using var client = host.CreateClient();

        // Act
        var response = await client.GetAsync(HealthPath);

        // Assert
        response.EnsureSuccessStatusCode();
        var logEvent = await FindRequestSummaryEventAsync(factory, HealthPath, seconds: 10);
        Assert.NotNull(logEvent);
        Assert.Equal(LogEventLevel.Debug, logEvent.Level);
    }

    [Fact]
    public async Task RequestLogging_EndpointException_LogsAtErrorLevel()
    {
        // Arrange: Create a test factory that replaces ICategoryCache with a fake that throws
        // a test-only exception not explicitly mapped by ApiExceptionMiddleware.
        // This causes the endpoint to return HTTP 500, and the request log should be Error level.
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();

        var factoryWithThrowingCache = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove the existing test cache and replace with one that throws
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICategoryCache));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddScoped<ICategoryCache>(_ => new ThrowingCategoryCache());
            });
        });

        using var client = factoryWithThrowingCache.CreateClient();

        // Act: Call /api/v1/categories, which will throw from the cache service
        var response = await client.GetAsync(CategoriesPath);

        // Assert: The unmapped exception should produce a 500
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);

        // Find the request summary event from the factory's sink, waiting for async emission
        var logEvent = await FindRequestSummaryEventAsync(factory, CategoriesPath, seconds: 10);

        Assert.NotNull(logEvent);
        Assert.Equal(LogEventLevel.Error, logEvent.Level);

        factoryWithThrowingCache.Dispose();
    }

    [Fact]
    public async Task RequestLogging_SentinelValues_ShouldNotLeakIntoSinkEvents()
    {
        // Arrange: the base factory's configuration (and shared Sink registration) runs first;
        // this derived host appends an in-memory value on top of it.
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();

        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Sentinel:Secret"] = SentinelConfig
                });
            });
        });

        // Prove the config value really exists in this host; otherwise the check would be vacuous.
        Assert.Equal(
            SentinelConfig,
            host.Services.GetRequiredService<IConfiguration>()["Sentinel:Secret"]);

        // WithWebHostBuilder returns the base factory type, so set the header manually.
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", SentinelToken);

        // Existing anonymous POST endpoint. An invalid email fails LoginCommandValidator
        // before the handler resolves the database, so the endpoint runs deterministically
        // in this factory (which registers both Npgsql and InMemory EF providers) and the
        // sentinel password is still bound and validated.
        using var response = await client.PostAsJsonAsync(
            AuthLoginPath,
            new LoginRequestDto("not-an-email", SentinelPassword));

        // Assert the request actually reached and executed the endpoint's validation path.
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", responseBody, StringComparison.Ordinal);

        // Wait for the request-summary event: request-scoped events (authentication, MediatR,
        // hosting) are emitted before it, so they are all in the sink once it appears.
        var summary = await FindRequestSummaryEventAsync(factory, AuthLoginPath, seconds: 10);
        Assert.NotNull(summary);
        Assert.Equal("POST", GetScalarValue(summary, "RequestMethod"));
        Assert.Equal("400", GetScalarValue(summary, "StatusCode"));

        var events = factory.Sink.Events;
        Assert.NotEmpty(events);
        AssertNoSentinelsLeaked(events);
    }

    private static HttpClient CreateClientAsAdmin(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var token = tokenGenerator.GenerateAccessToken("test-admin", "test@example.com", ["Admin"]);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Waits asynchronously for the Serilog request-completion event (the "HTTP {RequestMethod}
    /// {RequestPath} responded {StatusCode} in {Elapsed} ms" template). Events are emitted
    /// asynchronously relative to HTTP response completion, so we poll briefly instead of
    /// asserting immediately. Returns null if the event is not found within the timeout.
    /// </summary>
    private static async Task<LogEvent?> FindRequestSummaryEventAsync(SerilogWebApplicationFactory factory, string path, int seconds = 5)
    {
        for (var attempt = 0; attempt < seconds * 10; attempt++)
        {
            var logEvent = factory.Sink.Events.SingleOrDefault(logEvent =>
                GetScalarValue(logEvent, "RequestPath") == path &&
                logEvent.Properties.ContainsKey("RequestMethod") &&
                logEvent.Properties.ContainsKey("StatusCode") &&
                logEvent.Properties.ContainsKey("Elapsed"));

            if (logEvent != null)
            {
                return logEvent;
            }

            await Task.Delay(100);
        }

        return null;
    }

    private static string? GetScalarValue(LogEvent logEvent, string propertyName)
    {
        return logEvent.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;
    }

    /// <summary>
    /// ST-06: fails if any sentinel appears in the message template, the rendered message,
    /// any scalar/structured/sequence/dictionary property name or value, or the exception text
    /// of any captured event. The Console JSON formatter serializes these same fields.
    /// </summary>
    private static void AssertNoSentinelsLeaked(IReadOnlyList<LogEvent> events)
    {
        string[] sentinels = [SentinelToken, SentinelPassword, SentinelConfig];

        foreach (var logEvent in events)
        {
            var candidates = new List<string>();

            CollectStrings(logEvent.MessageTemplate.Text, candidates);
            CollectStrings(logEvent.RenderMessage(), candidates);

            foreach (var property in logEvent.Properties)
            {
                CollectStrings(property.Key, candidates);
                CollectPropertyValue(property.Value, candidates);
            }

            CollectStrings(logEvent.Exception?.ToString(), candidates);

            foreach (var sentinel in sentinels)
            {
                foreach (var candidate in candidates)
                {
                    Assert.DoesNotContain(sentinel, candidate, StringComparison.Ordinal);
                }
            }
        }
    }

    private static void CollectPropertyValue(LogEventPropertyValue value, List<string> candidates)
    {
        switch (value)
        {
            case ScalarValue scalar:
                CollectStrings(scalar.Value?.ToString(), candidates);
                break;
            case StructureValue structure:
                CollectStrings(structure.TypeTag, candidates);
                foreach (var property in structure.Properties)
                {
                    CollectStrings(property.Name, candidates);
                    CollectPropertyValue(property.Value, candidates);
                }
                break;
            case SequenceValue sequence:
                foreach (var element in sequence.Elements)
                {
                    CollectPropertyValue(element, candidates);
                }
                break;
            case DictionaryValue dictionary:
                foreach (var element in dictionary.Elements)
                {
                    CollectPropertyValue(element.Key, candidates);
                    CollectPropertyValue(element.Value, candidates);
                }
                break;
            default:
                CollectStrings(value.ToString(), candidates);
                break;
        }
    }

    private static void CollectStrings(string? value, List<string> candidates)
    {
        if (!string.IsNullOrEmpty(value))
        {
            candidates.Add(value);
        }
    }

    /// <summary>
    /// Fake implementation of ICategoryCache that throws a test-only unmapped exception.
    /// ST05UnmappedTestException is not explicitly handled by ApiExceptionMiddleware, so it
    /// falls through to the default 500 response (line 29 of ApiExceptionMiddleware.cs).
    /// Used to test error-level logging when an endpoint's dependency fails with a 500.
    /// </summary>
    private sealed class ThrowingCategoryCache : ICategoryCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            throw new ST05UnmappedTestException("Simulated cache failure for testing (ST-05)");
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            throw new ST05UnmappedTestException("Simulated cache failure for testing (ST-05)");
        }

        public Task InvalidateAsync(CancellationToken cancellationToken = default)
        {
            throw new ST05UnmappedTestException("Simulated cache failure for testing (ST-05)");
        }
    }

    /// <summary>
    /// Test-only exception that is not explicitly mapped by ApiExceptionMiddleware.
    /// When thrown and not caught, it falls through to the default 500 response.
    /// Used exclusively by ST-05 to verify error-level logging for HTTP 500 responses.
    /// </summary>
    private sealed class ST05UnmappedTestException : Exception
    {
        public ST05UnmappedTestException(string message) : base(message) { }
    }
}
