using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeStepEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory factory;

    public RecipeStepEndpointsTests(CustomAuthWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task StepRoutes_RequireAuthentication()
    {
        var client = factory.CreateIsolatedClient();
        var recipeId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var body = StepRequest();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync($"/api/v1/recipes/{recipeId}/steps", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync($"/api/v1/recipes/{recipeId}/steps/{stepId}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/v1/recipes/{recipeId}/steps/{stepId}")).StatusCode);
    }

    [Fact]
    public async Task AddStep_InvalidContent_Returns400ProblemDetails()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/steps", new
        {
            title = " ", description = " ", durationMinutes = -2, imageUrl = (string?)null,
            stepNumber = 900
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Title", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Description", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DurationMinutes", body, StringComparison.OrdinalIgnoreCase);
    }

    private static object StepRequest() => new
    {
        title = "Heat", description = "Heat the pan.", durationMinutes = (int?)1,
        imageUrl = (string?)null
    };

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"step_{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Step Tester"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }
}
