using System.Net;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// HT-11: Verifies that health routes are exempt from the GlobalLimiter (assumption A5).
/// Uses X-Test-Client-IP header to create isolated rate-limit partitions per route.
/// </summary>
public sealed class HealthRateLimitingTests
{
    private const int RequestCount = 105; // Exceeds the 100 req/min GlobalLimiter
    private const int GlobalLimiterPermitLimit = 100;

    /// <summary>
    /// Each health route must NOT return 429 when the GlobalLimiter is saturated
    /// for that IP partition. Any status other than 429 (including 503) is acceptable.
    /// </summary>
    [Theory]
    [InlineData("/health/live", "192.0.2.11")]
    [InlineData("/health/ready", "192.0.2.22")]
    [InlineData("/health", "192.0.2.33")]
    public async Task HealthRoute_ShouldNotBeRateLimited_WhenExceedingGlobalLimit(
        string route,
        string testIp)
    {
        // Arrange
        using var factory = new ObservabilityWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", testIp);

        // Act: Send 105 requests (exceeds 100 req/min GlobalLimiter)
        for (int i = 0; i < RequestCount; i++)
        {
            var response = await client.GetAsync(route);

            // Assert: No 429 allowed (503 or other statuses are fine - probes check dependencies)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    /// <summary>
    /// Control test: Proves the GlobalLimiter is actually active in the test host.
    /// A non-exempt GET endpoint must return 429 after 100 requests from the same IP.
    /// </summary>
    [Fact]
    public async Task Control_NonHealthEndpoint_ShouldReturn429_WhenExceedingGlobalLimit()
    {
        // Arrange
        using var factory = new ObservabilityWebApplicationFactory();
        using var client = factory.CreateClient();
        var controlIp = "192.0.2.99"; // Isolated IP for control test
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", controlIp);

        var received429 = false;

        // Act: Send 105 requests to the root endpoint (covered by GlobalLimiter)
        for (int i = 0; i < RequestCount; i++)
        {
            var response = await client.GetAsync("/");

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                received429 = true;
                break;
            }
        }

        // Assert: At least one 429 proves GlobalLimiter is active
        Assert.True(received429,
            $"Control test failed: expected at least one 429 after {RequestCount} requests " +
            $"to a non-exempt endpoint with GlobalLimiter limit of {GlobalLimiterPermitLimit}");
    }
}
