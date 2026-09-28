using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeStepEndpointsTests : IClassFixture<RecipeStepWebApplicationFactory>
{
    private readonly RecipeStepWebApplicationFactory factory;

    public RecipeStepEndpointsTests(RecipeStepWebApplicationFactory factory) => this.factory = factory;

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
        var (client, _) = await CreateAuthenticatedClientAsync();
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

    [Fact]
    public async Task StepLifecycle_CreatesUpdatesAndSoftDeletesThroughApi()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync();
        var recipe = await SeedRecipeAsync(userId);

        var create = await client.PostAsJsonAsync($"/api/v1/recipes/{recipe.Id}/steps", new
        {
            title = "  Boil  ", description = "  Boil water.  ", durationMinutes = (int?)3,
            imageUrl = (string?)""
        });
        Assert.True(create.StatusCode == HttpStatusCode.Created,
            $"Expected Created, received {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        var created = await create.Content.ReadFromJsonAsync<StepResponse>();
        Assert.NotNull(created);
        Assert.Equal(1, created!.StepNumber);
        Assert.Equal("Boil", created.Title);
        Assert.Equal("Boil water.", created.Description);
        Assert.Null(created.ImageUrl);

        var update = await client.PutAsJsonAsync($"/api/v1/recipes/{recipe.Id}/steps/{created.Id}", StepRequest());
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<StepResponse>();
        Assert.NotNull(updated);
        Assert.Equal(1, updated!.StepNumber);
        Assert.Equal("Heat", updated.Title);

        var delete = await client.DeleteAsync($"/api/v1/recipes/{recipe.Id}/steps/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var detail = await client.GetFromJsonAsync<RecipeDetailResponse>($"/api/v1/recipes/{recipe.Slug}");
        Assert.NotNull(detail);
        Assert.DoesNotContain(detail!.Steps, item => item.Id == created.Id);
    }

    private static object StepRequest() => new
    {
        title = "Heat", description = "Heat the pan.", durationMinutes = (int?)1,
        imageUrl = (string?)null
    };

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"step_{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Step Tester"));
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
        var category = Category.Create($"Step Category {Guid.NewGuid():N}", null);
        var recipe = new Recipe
        {
            Title = "Step Test Soup", Slug = $"step-test-{Guid.NewGuid():N}",
            CategoryId = category.Id, AuthorId = userId, Difficulty = RecipeDifficulty.Easy, Servings = 2
        };
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Categories.Add(category);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return (recipe.Id, recipe.Slug);
    }

    private sealed record StepResponse(Guid Id, int StepNumber, string? Title, string Description, int? DurationMinutes, string? ImageUrl);
    private sealed record RecipeDetailResponse(IReadOnlyList<StepResponse> Steps);
}

public sealed class RecipeStepWebApplicationFactory : CustomAuthWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRecipeCache>();
            services.AddSingleton<IRecipeCache, TestRecipeCache>();
            services.RemoveAll<IRecipeMutationLock>();
            services.AddSingleton<IRecipeMutationLock, TestRecipeMutationLock>();
        });
    }

    private sealed class TestRecipeCache : IRecipeCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestRecipeMutationLock : IRecipeMutationLock
    {
        public Task<IRecipeMutationLease> AcquireAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IRecipeMutationLease>(new TestRecipeMutationLease());
    }

    private sealed class TestRecipeMutationLease : IRecipeMutationLease
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
