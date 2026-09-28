using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeCreationEndpointsTests : IClassFixture<RecipeStepWebApplicationFactory>
{
    private readonly RecipeStepWebApplicationFactory factory;

    public RecipeCreationEndpointsTests(RecipeStepWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task CreateRecipe_WithVietnameseTitle_ReturnsCreatedWithAsciiSlugLocation()
    {
        var client = factory.CreateIsolatedClient();
        var email = $"recipe_{Guid.NewGuid():N}@example.com";
        const string password = "P@ssword123";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Recipe Tester"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var category = Category.Create($"Recipe Category {Guid.NewGuid():N}", null);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/v1/recipes", new
        {
            title = "Canh chua cá", description = "Canh chua nấu với cá.", categoryId = category.Id,
            prepTimeMinutes = 15, cookTimeMinutes = 30, servings = 4, difficulty = "Easy"
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected Created, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal("/api/v1/recipes/canh-chua-ca", response.Headers.Location?.OriginalString);
    }
}
