using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class AdminUserEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory factory;

    public AdminUserEndpointsTests(CustomAuthWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task GetUsers_AnonymousRequest_ReturnsUnauthorized()
    {
        using var client = factory.CreateIsolatedClient();

        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AdminRequest_ReturnsAllUsers()
    {
        using var client = CreateClientAs("Admin");

        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<IReadOnlyList<AdminUserDto>>();
        Assert.NotNull(users);
    }

    private HttpClient CreateClientAs(string role)
    {
        var client = factory.CreateIsolatedClient();
        using var scope = factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var token = tokenGenerator.GenerateAccessToken(
            $"test-{role.ToLowerInvariant()}",
            "test@example.com",
            [role]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
