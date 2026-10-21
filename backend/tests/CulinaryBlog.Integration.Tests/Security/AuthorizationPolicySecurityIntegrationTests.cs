using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Integration.Tests.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

/// <summary>
/// NFR-SEC-006 Integration Tests:
/// Verifies that endpoints protected by authentication/authorization return
/// HTTP 401 Unauthorized for unauthenticated requests,
/// HTTP 403 Forbidden for insufficient permissions / non-owner writes,
/// and that named authorization policies (AuthorPolicy, AdminPolicy) resolve
/// and evaluate correctly through the real DI container.
/// </summary>
public class AuthorizationPolicySecurityIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public AuthorizationPolicySecurityIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DiResolution_AuthorPolicy_ShouldSucceedForAuthorAndAdmin_FailForReaderAndAnonymous()
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var authorPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "author-1"), new Claim(ClaimTypes.Role, AppRoles.Author) }, "TestAuth"));
        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Role, AppRoles.Admin) }, "TestAuth"));
        var readerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "reader-1"), new Claim(ClaimTypes.Role, AppRoles.Reader) }, "TestAuth"));
        var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var authorResult = await authService.AuthorizeAsync(authorPrincipal, AppPolicies.AuthorPolicy);
        var adminResult = await authService.AuthorizeAsync(adminPrincipal, AppPolicies.AuthorPolicy);
        var readerResult = await authService.AuthorizeAsync(readerPrincipal, AppPolicies.AuthorPolicy);
        var anonResult = await authService.AuthorizeAsync(anonPrincipal, AppPolicies.AuthorPolicy);

        authorResult.Succeeded.Should().BeTrue("Author role must satisfy AuthorPolicy");
        adminResult.Succeeded.Should().BeTrue("Admin role must satisfy AuthorPolicy");
        readerResult.Succeeded.Should().BeFalse("Reader role must NOT satisfy AuthorPolicy");
        anonResult.Succeeded.Should().BeFalse("Unauthenticated user must NOT satisfy AuthorPolicy");
    }

    [Fact]
    public async Task DiResolution_AdminPolicy_ShouldSucceedOnlyForAdmin()
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Role, AppRoles.Admin) }, "TestAuth"));
        var authorPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "author-1"), new Claim(ClaimTypes.Role, AppRoles.Author) }, "TestAuth"));
        var readerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "reader-1"), new Claim(ClaimTypes.Role, AppRoles.Reader) }, "TestAuth"));
        var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var adminResult = await authService.AuthorizeAsync(adminPrincipal, AppPolicies.AdminPolicy);
        var authorResult = await authService.AuthorizeAsync(authorPrincipal, AppPolicies.AdminPolicy);
        var readerResult = await authService.AuthorizeAsync(readerPrincipal, AppPolicies.AdminPolicy);
        var anonResult = await authService.AuthorizeAsync(anonPrincipal, AppPolicies.AdminPolicy);

        adminResult.Succeeded.Should().BeTrue("Admin role must satisfy AdminPolicy");
        authorResult.Succeeded.Should().BeFalse("Author role must NOT satisfy AdminPolicy");
        readerResult.Succeeded.Should().BeFalse("Reader role must NOT satisfy AdminPolicy");
        anonResult.Succeeded.Should().BeFalse("Unauthenticated user must NOT satisfy AdminPolicy");
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateClient();
        var request = new UpdateProfileRequestDto("New Name", null, "New bio");

        var response = await client.PatchAsJsonAsync("/api/v1/auth/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Unauthenticated_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateRecipe_Unauthenticated_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateClient();
        var content = JsonContent.Create(new { title = "Sample Recipe", categoryId = Guid.NewGuid() });

        var response = await client.PostAsync("/api/v1/recipes", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteRecipe_Unauthenticated_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/recipes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_CategoryCreation_NonAdminUser_ShouldReturn403Forbidden()
    {
        // Category creation requires Admin role per CategoryEndpoints.cs
        var client = _factory.CreateIsolatedClient();
        var email = $"regular_user_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123";

        var regRes = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Regular User"));
        regRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var authData = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        authData.Should().NotBeNull();

        // Attach token of regular user (Author/Reader role, not Admin)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData!.AccessToken);

        // Attempt admin-only category creation
        var catContent = JsonContent.Create(new { name = "Unauthorized Category" });
        var catResponse = await client.PostAsync("/api/v1/categories", catContent);

        // Must be rejected with 403 Forbidden
        catResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
