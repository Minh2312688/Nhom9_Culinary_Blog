using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class RecipeCreationEndpointsTests
{
    [Fact]
    public async Task PostRecipe_WithVietnameseTitle_ShouldReturn201WithSlugLocation()
    {
        await using var factory = new CategoryWebApplicationFactory();
        await factory.SeedAsync(async context =>
        {
            context.Categories.Add(Category.Create("Món chính"));
            await context.SaveChangesAsync();
        });
        var categoryId = factory.Query(context => context.Categories.Single().Id);
        var client = factory.CreateClientAs("Author");

        var response = await client.PostAsJsonAsync(
            "/api/v1/recipes",
            new
            {
                title = "Bún bò Huế",
                description = "Món ăn đặc sản",
                categoryId,
                prepTimeMinutes = 20,
                cookTimeMinutes = 90,
                servings = 4,
                difficulty = "Medium"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be("/api/v1/recipes/bun-bo-hue");
    }
}
