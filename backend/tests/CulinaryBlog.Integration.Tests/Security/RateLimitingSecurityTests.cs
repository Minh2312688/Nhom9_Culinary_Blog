using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Integration.Tests.Endpoints;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

/// <summary>
/// NFR-SEC-003 Security Integration Tests:
/// - AuthRateLimitPolicy: 10 requests / minute / IP (Sliding Window)
/// - UploadRateLimitPolicy: 5 requests / minute / IP (Fixed Window)
/// - GlobalLimiter: 100 requests / minute / IP (Fixed Window)
/// - 429 Too Many Requests response with RFC7807 ProblemDetails and Retry-After header
/// - IP partition isolation between different client IP addresses
/// - Policy composition: Auth & Upload limits are enforced before Global limit
/// </summary>
public class RateLimitingSecurityTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public RateLimitingSecurityTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthEndpoint_RateLimiting_ShouldPermit10Requests_AndReject11thWith429AndRetryAfter()
    {
        // Arrange
        var testIp = "192.0.2.11"; // Distinct documentation test IP
        var client = _factory.CreateClientWithIp(testIp);

        // Act: Send 10 requests (within limit)
        for (int i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequestDto("nobody@example.com", "WrongPassword!123"));

            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"Request {i + 1} should be permitted by Auth rate limiter");
        }

        // Act: 11th request exceeds 10 req/min limit
        var exceededResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto("nobody@example.com", "WrongPassword!123"));

        // Assert
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "11th Auth request from same IP within window must be rejected with 429");

        exceededResponse.Headers.Contains("Retry-After").Should().BeTrue(
            "429 response must contain Retry-After header");
        exceededResponse.Headers.GetValues("Retry-After").First().Should().NotBeNullOrWhiteSpace();

        var body = await exceededResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Too Many Requests");
        exceededResponse.Content.Headers.ContentType?.MediaType.Should().BeOneOf("application/problem+json", "application/json");
    }

    [Fact]
    public async Task UploadEndpoint_RateLimiting_ShouldPermit5Requests_AndReject6thWith429AndRetryAfter()
    {
        // Arrange
        var testIp = "192.0.2.22";
        var client = _factory.CreateClientWithIp(testIp);
        var recipeId = Guid.NewGuid();

        // Act: Send 5 upload requests (within 5 req/min limit)
        for (int i = 0; i < 5; i++)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "test.jpg");

            var response = await client.PostAsync($"/api/v1/recipes/{recipeId}/images", form);

            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"Upload request {i + 1} should be permitted by Upload rate limiter");
        }

        // Act: 6th request exceeds 5 req/min limit
        using var exceededForm = new MultipartFormDataContent();
        exceededForm.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "test.jpg");
        var exceededResponse = await client.PostAsync($"/api/v1/recipes/{recipeId}/images", exceededForm);

        // Assert
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "6th Upload request from same IP within window must be rejected with 429");

        exceededResponse.Headers.Contains("Retry-After").Should().BeTrue(
            "429 response must contain Retry-After header");
        exceededResponse.Headers.GetValues("Retry-After").First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GeneralApi_RateLimiting_ShouldPermit100Requests_AndReject101thWith429AndRetryAfter()
    {
        // Arrange: Test endpoint covered by GlobalLimiter
        var testIp = "192.0.2.33";
        var client = _factory.CreateClientWithIp(testIp);

        // Act: Send 100 requests (within 100 req/min Global limit)
        for (int i = 0; i < 100; i++)
        {
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"General request {i + 1} should succeed under GlobalLimiter");
        }

        // Act: 101st request exceeds 100 req/min Global limit
        var exceededResponse = await client.GetAsync("/");

        // Assert
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "101st request from same IP within window must be rejected with 429");

        exceededResponse.Headers.Contains("Retry-After").Should().BeTrue(
            "429 response must contain Retry-After header");
    }

    [Fact]
    public async Task RateLimiting_IpPartitionIsolation_WhenOneIpExhausted_DifferentIpRemainsPermitted()
    {
        // Arrange
        var ipA = "192.0.2.44";
        var ipB = "192.0.2.55";

        var clientA = _factory.CreateClientWithIp(ipA);
        var clientB = _factory.CreateClientWithIp(ipB);

        // Exhaust IP A on Auth (10 requests)
        for (int i = 0; i < 10; i++)
        {
            await clientA.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequestDto("test@example.com", "Password123!"));
        }

        // IP A 11th request -> 429
        var respA = await clientA.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto("test@example.com", "Password123!"));
        respA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // IP B request -> must NOT be rate limited
        var respB = await clientB.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto("test@example.com", "Password123!"));
        respB.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            "Independent IP B must have its own fresh rate limit partition");
    }

    [Fact]
    public async Task PolicyComposition_EndpointLimitersAreStrictlyEnforcedBeforeGlobalLimit()
    {
        // Arrange
        var authIp = "192.0.2.66";
        var uploadIp = "192.0.2.77";

        var authClient = _factory.CreateClientWithIp(authIp);
        var uploadClient = _factory.CreateClientWithIp(uploadIp);

        // 1. Verify Auth is blocked at request 11 (far before 100 global limit)
        for (int i = 0; i < 10; i++)
        {
            await authClient.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequestDto("test@example.com", "Password123!"));
        }
        var auth11 = await authClient.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto("test@example.com", "Password123!"));
        auth11.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "Auth policy must trigger at 10 requests, not defer to 100 global limit");

        // 2. Verify Upload is blocked at request 6 (far before 100 global limit)
        var recipeId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "img.jpg");
            await uploadClient.PostAsync($"/api/v1/recipes/{recipeId}/images", form);
        }
        using var form6 = new MultipartFormDataContent();
        form6.Add(new ByteArrayContent([0xff, 0xd8, 0xff]), "file", "img.jpg");
        var upload6 = await uploadClient.PostAsync($"/api/v1/recipes/{recipeId}/images", form6);
        upload6.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "Upload policy must trigger at 5 requests, not defer to 100 global limit");
    }
}
