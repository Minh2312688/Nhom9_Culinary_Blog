using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeIngredientEndpointsTests : IClassFixture<RecipeIngredientWebApplicationFactory>
{
    private readonly RecipeIngredientWebApplicationFactory factory;

    public RecipeIngredientEndpointsTests(RecipeIngredientWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task IngredientRoutes_RequireAuthentication()
    {
        var client = factory.CreateIsolatedClient();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var body = IngredientRequest("Salt", 1, "g", null, 0);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync($"/api/v1/recipes/{recipeId}/ingredients", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync($"/api/v1/recipes/{recipeId}/ingredients/{ingredientId}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/v1/recipes/{recipeId}/ingredients/{ingredientId}")).StatusCode);
    }

    [Fact]
    public async Task IngredientLifecycle_CreatesUpdatesAndSoftDeletesThroughApi()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync();
        var recipe = await SeedRecipeAsync(userId);

        var create = await client.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients",
            IngredientRequest("  Salt  ", 1.5m, " g ", "  fine  ", 0));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<IngredientResponse>();
        Assert.NotNull(created);
        Assert.Equal("Salt", created!.Name);
        Assert.Equal("g", created.Unit);
        Assert.Equal("fine", created.Notes);

        var update = await client.PutAsJsonAsync($"/api/v1/recipes/{recipe.Id}/ingredients/{created.Id}",
            IngredientRequest("Pepper", null, null, null, 1));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<IngredientResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Pepper", updated!.Name);
        Assert.Null(updated.Quantity);
        Assert.Equal(1, updated.OrderIndex);

        var delete = await client.DeleteAsync($"/api/v1/recipes/{recipe.Id}/ingredients/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var detail = await client.GetFromJsonAsync<RecipeDetailResponse>($"/api/v1/recipes/{recipe.Slug}");
        Assert.NotNull(detail);
        Assert.DoesNotContain(detail!.Ingredients, item => item.Id == created.Id);
    }

    [Fact]
    public async Task AddIngredient_InvalidPayload_Returns400ProblemDetails()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/ingredients",
            IngredientRequest(" ", 0, null, null, -1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quantity", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OrderIndex", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngredientRoutes_Return403ForAnotherAuthorsRecipeAnd404ForMissingChildren()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync();
        var ownedRecipe = await SeedRecipeAsync(userId);
        var otherAuthorsRecipe = await SeedRecipeAsync("different-author");

        var forbidden = await client.PostAsJsonAsync($"/api/v1/recipes/{otherAuthorsRecipe.Id}/ingredients",
            IngredientRequest("Salt", null, null, null, 0));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var missingRecipe = await client.PostAsJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/ingredients",
            IngredientRequest("Salt", null, null, null, 0));
        Assert.Equal(HttpStatusCode.NotFound, missingRecipe.StatusCode);

        var missingIngredient = Guid.NewGuid();
        var update = await client.PutAsJsonAsync($"/api/v1/recipes/{ownedRecipe.Id}/ingredients/{missingIngredient}",
            IngredientRequest("Salt", null, null, null, 0));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        var delete = await client.DeleteAsync($"/api/v1/recipes/{ownedRecipe.Id}/ingredients/{missingIngredient}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"ingredient_{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequestDto(email, password, "Ingredient Tester"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registeredUser = await register.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(registeredUser);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return (client, registeredUser!.UserId);
    }

    private async Task<(Guid Id, string Slug)> SeedRecipeAsync(string userId)
    {
        var category = Category.Create($"Ingredient Category {Guid.NewGuid():N}", null);
        var recipe = new Recipe
        {
            Title = "Ingredient Test Soup", Slug = $"ingredient-test-{Guid.NewGuid():N}",
            CategoryId = category.Id, AuthorId = userId, Difficulty = RecipeDifficulty.Easy, Servings = 2
        };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Categories.Add(category);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return (recipe.Id, recipe.Slug);
    }

    private static object IngredientRequest(string name, decimal? quantity, string? unit, string? notes, int orderIndex) => new
    {
        name, quantity, unit, notes, orderIndex
    };

    private sealed record IngredientResponse(Guid Id, string Name, decimal? Quantity, string? Unit, string? Notes, int OrderIndex);
    private sealed record RecipeDetailResponse(IReadOnlyList<IngredientResponse> Ingredients);
}

public sealed class RecipeIngredientWebApplicationFactory : CustomAuthWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRecipeCache>();
            services.AddSingleton<IRecipeCache, TestRecipeCache>();
        });
    }

    private sealed class TestRecipeCache : IRecipeCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
