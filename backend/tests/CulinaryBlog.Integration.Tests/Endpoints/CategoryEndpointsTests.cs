using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

/// <summary>
/// API test cho /api/v1/categories và /api/v1/categories/{slug}: status code,
/// Problem Details và flat pagination shape đúng contract nhóm đã chốt.
/// </summary>
public class CategoryEndpointsTests
{
    private static Task SeedTwoCategoriesAsync(CategoryWebApplicationFactory factory) =>
        factory.SeedAsync(async context =>
        {
            context.Categories.AddRange(
                Category.Create("Bánh Ngọt", "Mô tả 1"),
                Category.Create("Cà Phê Sữa Đá", "Mô tả 2"));
            await context.SaveChangesAsync();
        });

    [Fact]
    public async Task GetCategories_ShouldReturnFlatPaginationShape()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        await SeedTwoCategoriesAsync(factory);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/categories?page=1&pageSize=1&sortBy=name&sortOrder=asc");

        // Assert: shape phẳng đúng contract
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("items").GetArrayLength().Should().Be(1);
        root.GetProperty("totalCount").GetInt32().Should().Be(2);
        root.GetProperty("page").GetInt32().Should().Be(1);
        root.GetProperty("pageSize").GetInt32().Should().Be(1);
        root.GetProperty("totalPages").GetInt32().Should().Be(2);
        root.GetProperty("hasNextPage").GetBoolean().Should().BeTrue();
        root.GetProperty("hasPreviousPage").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetCategories_WithInvalidSortOrder_ShouldReturn400ProblemDetails()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/categories?sortOrder=sideways");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PostCategory_ShouldReturn201WithLocationAndGeneratedSlug()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new { name = "  Món Chay  ", description = "  Thanh đạm  " });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be("/api/v1/categories/mon-chay");
        var body = await response.Content.ReadFromJsonAsync<CategoryDto>();
        body!.Slug.Should().Be("mon-chay");
        body.Name.Should().Be("Món Chay");
        body.Description.Should().Be("Thanh đạm");
    }

    [Fact]
    public async Task PostCategory_WithInvalidName_ShouldReturn400ProblemDetails()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new { name = "A", description = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task PostCategory_WithDuplicateName_ShouldReturn409()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        await factory.SeedAsync(async context =>
        {
            context.Categories.Add(Category.Create("Bánh Ngọt"));
            await context.SaveChangesAsync();
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new { name = "  bánh ngọt  ", description = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetCategoryBySlug_ShouldReturn200AndUnknownSlugShouldReturn404()
    {
        // Arrange
        await using var factory = new CategoryWebApplicationFactory();
        await SeedTwoCategoriesAsync(factory);
        var client = factory.CreateClient();

        // Act
        var found = await client.GetAsync("/api/v1/categories/ca-phe-sua-da");
        var missing = await client.GetAsync("/api/v1/categories/khong-ton-tai");

        // Assert
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<CategoryDto>())!.Slug.Should().Be("ca-phe-sua-da");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}