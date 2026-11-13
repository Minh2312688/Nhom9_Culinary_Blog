using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeEndpointsTests
{
    [Fact]
    public async Task CreateRecipe_WithVietnameseTitle_ShouldReturnSlugInLocationHeader()
    {
        await using var factory = new CategoryWebApplicationFactory();
        var category = Category.Create("Món Việt", null);
        await factory.SeedAsync(async context =>
        {
            context.Categories.Add(category);
            await context.SaveChangesAsync();
        });
        var client = factory.CreateClientAs("Author");

        var response = await client.PostAsJsonAsync("/api/v1/recipes", new
        {
            title = "Bánh mì cá hộp",
            description = "Món ăn thử",
            categoryId = category.Id,
            prepTimeMinutes = 5,
            cookTimeMinutes = 10,
            servings = 2,
            difficulty = "Easy",
            ingredients = Array.Empty<object>(),
            steps = Array.Empty<object>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be("/api/v1/recipes/banh-mi-ca-hop");
    }
}
