using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeIngredientEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory factory;

    public RecipeIngredientEndpointsTests(CustomAuthWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task IngredientRoutes_RequireAuthentication()
    {
        var client = factory.CreateIsolatedClient();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync($"/api/v1/recipes/{recipeId}/ingredients", ValidIngredient())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync($"/api/v1/recipes/{recipeId}/ingredients/{ingredientId}", ValidIngredient())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/v1/recipes/{recipeId}/ingredients/{ingredientId}")).StatusCode);
    }

    [Fact]
    public async Task AddIngredient_InvalidQuantity_Returns400ProblemDetails()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"ingredient_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Ingredient Tester"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await client.PostAsJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/ingredients", new
        {
            name = "Salt", quantity = 0, unit = "tsp", notes = (string?)null, orderIndex = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quantity", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddIngredient_MissingOrderIndex_Returns400ProblemDetails()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/ingredients", new
        {
            name = "Salt", quantity = (decimal?)null, unit = (string?)null, notes = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("OrderIndex", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static object ValidIngredient() => new
    {
        name = "Salt", quantity = (decimal?)1, unit = "tsp", notes = (string?)null, orderIndex = 0
    };

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"ingredient_{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequestDto(email, password, "Ingredient Tester"))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }
}
