using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public class GlobalExceptionMiddlewareTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public GlobalExceptionMiddlewareTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthorizedException_ShouldReturn401ProblemDetails_WithRfc7807Structure()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();

        // Act: Call refresh with an unknown token to throw UnauthorizedException from handler
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto("invalid_refresh_token_for_test"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Contain("Invalid or expired refresh token");
        problem.Instance.Should().Be("/api/v1/auth/refresh");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task NotFoundException_ShouldReturn404ProblemDetails_WithRfc7807Structure()
    {
        // Arrange: Login as user, then delete user from DbContext so me endpoint throws NotFoundException
        var client = _factory.CreateIsolatedClient();
        var email = $"notfound_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "To Be Deleted"));
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlog.Infrastructure.Persistence.AuthDbContext>();
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                db.Users.Remove(user);
                await db.SaveChangesAsync();
            }
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        // Act
        var response = await client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Contain("User not found");
        problem.Instance.Should().Be("/api/v1/auth/me");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task DomainException_ShouldReturn400ProblemDetails_WithoutLeakingStackTraces()
    {
        // Arrange: Test endpoint in Development/Test environment
        var client = _factory.CreateIsolatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/test/throw-domain");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Domain Rule Violation");
        problem.Detail.Should().Be("Simulated domain rule violation.");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task UnexpectedException_ShouldReturn500ProblemDetails_WithoutLeakingInternalStackTrace()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/test/throw-500");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(500);
        problem.Title.Should().Be("Internal Server Error");
        problem.Detail.Should().Be("An unexpected error occurred while processing your request.");
        problem.Extensions.Should().ContainKey("traceId");

        // Verify NO stack trace or internal message leaked
        var rawContent = await response.Content.ReadAsStringAsync();
        rawContent.Should().NotContain("Simulated unexpected crash");
        rawContent.Should().NotContain("at CulinaryBlog");
        rawContent.Should().NotContain("line ");
    }
}
