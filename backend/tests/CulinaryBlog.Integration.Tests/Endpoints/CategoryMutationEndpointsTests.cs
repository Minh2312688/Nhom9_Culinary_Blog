using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

/// <summary>
/// API test cho PUT/DELETE /api/v1/categories/{id}: cập nhật 4 field, giữ nguyên slug,
/// soft delete và delete guard 409 khi category còn recipe đang hoạt động.
/// </summary>
public class CategoryMutationEndpointsTests
{
    [Fact]
    public async Task PutCategory_ShouldUpdateFourFieldsAndKeepSlug()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        await factory.SeedAsync(async context =>
        {
            context.Categories.Add(Category.Create("Bánh Ngọt", "Mô tả cũ"));
            await context.SaveChangesAsync();
        });
        var id = factory.Query(context => context.Categories.Single().Id);
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/categories/{id}",
            new
            {
                name = "Bánh Ngọt Mới",
                description = "Mô tả mới",
                imageUrl = "https://cdn.test/a.jpg",
                orderIndex = 5
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CategoryDto>();
        body!.Slug.Should().Be("banh-ngot");
        body.Name.Should().Be("Bánh Ngọt Mới");
        body.Description.Should().Be("Mô tả mới");
        body.ImageUrl.Should().Be("https://cdn.test/a.jpg");
        body.OrderIndex.Should().Be(5);
        factory.Query(context => context.Categories.Single().Slug).Should().Be("banh-ngot");
    }

    [Fact]
    public async Task PutCategory_UnknownId_ShouldReturn404()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/categories/{Guid.NewGuid()}",
            new
            {
                name = "Bánh Ngọt",
                description = (string?)null,
                imageUrl = (string?)null,
                orderIndex = 0
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task DeleteCategory_ShouldSoftDeleteAndHideFromList()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        await factory.SeedAsync(async context =>
        {
            context.Categories.Add(Category.Create("Bánh Ngọt"));
            await context.SaveChangesAsync();
        });
        var id = factory.Query(context => context.Categories.Single().Id);
        var client = factory.CreateClient();

        // Act
        var deleted = await client.DeleteAsync($"/api/v1/categories/{id}");
        var list = await client.GetAsync("/api/v1/categories");
        var secondDelete = await client.DeleteAsync($"/api/v1/categories/{id}");

        // Assert: soft delete, row còn trong DB nhưng không xuất hiện ở query thường
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
        factory.Query(context => context.Categories.IgnoreQueryFilters().Single().IsDeleted)
            .Should().BeTrue();
        secondDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCategory_WithActiveRecipe_ShouldReturn409()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        await factory.SeedAsync(async context =>
        {
            var category = Category.Create("Bánh Ngọt");
            context.Categories.Add(category);
            context.Recipes.Add(new Recipe
            {
                Title = "Bánh Flan",
                Slug = "banh-flan",
                CategoryId = category.Id,
                AuthorId = "test-author"
            });
            await context.SaveChangesAsync();
        });
        var id = factory.Query(context => context.Categories.Single().Id);
        var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/api/v1/categories/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        factory.Query(context => context.Categories.IgnoreQueryFilters().Single().IsDeleted)
            .Should().BeFalse();
    }
}