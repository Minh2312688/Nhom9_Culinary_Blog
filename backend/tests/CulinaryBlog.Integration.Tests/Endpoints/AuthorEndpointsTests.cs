using System.Net;
using System.Text.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public sealed class AuthorEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory factory;

    public AuthorEndpointsTests(CustomAuthWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task GetAuthorProfile_ReturnsAllowlistedProfileAndOnlyThatAuthorsPublishedRecipes()
    {
        var authorId = $"author-{Guid.NewGuid():N}";
        var otherAuthorId = $"author-{Guid.NewGuid():N}";
        var categoryId = Guid.NewGuid();
        var newestId = Guid.NewGuid();
        var olderId = Guid.NewGuid();
        await SeedAsync(
            CreateAuthor(authorId, "Public Author"),
            CreateAuthor(otherAuthorId, "Other Author"),
            new Category { Id = categoryId, Name = "Test", Slug = $"test-{Guid.NewGuid():N}" },
            new Recipe
            {
                Id = newestId, Title = "Newest", Slug = $"newest-{Guid.NewGuid():N}", CategoryId = categoryId,
                AuthorId = authorId, Status = RecipeStatus.Published,
                PublishedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)
            },
            new Recipe
            {
                Id = olderId, Title = "Older", Slug = $"older-{Guid.NewGuid():N}", CategoryId = categoryId,
                AuthorId = authorId, Status = RecipeStatus.Published,
                PublishedAt = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero)
            },
            new Recipe
            {
                Title = "Draft", Slug = $"draft-{Guid.NewGuid():N}", CategoryId = categoryId,
                AuthorId = authorId, Status = RecipeStatus.Draft
            },
            new Recipe
            {
                Title = "Archived", Slug = $"archived-{Guid.NewGuid():N}", CategoryId = categoryId,
                AuthorId = authorId, Status = RecipeStatus.Archived
            },
            new Recipe
            {
                Title = "Other author", Slug = $"other-{Guid.NewGuid():N}", CategoryId = categoryId,
                AuthorId = otherAuthorId, Status = RecipeStatus.Published
            });

        var response = await factory.CreateIsolatedClient()
            .GetAsync($"/api/v1/authors/{authorId}?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(
            new[] { "id", "displayName", "avatarUrl", "bio", "recipes" }.Order(),
            root.EnumerateObject().Select(property => property.Name).Order());
        Assert.False(root.TryGetProperty("email", out _));
        Assert.False(root.TryGetProperty("roles", out _));
        Assert.Equal(authorId, root.GetProperty("id").GetString());
        Assert.Equal("Public Author", root.GetProperty("displayName").GetString());
        Assert.Equal("https://example.test/avatar.png", root.GetProperty("avatarUrl").GetString());
        Assert.Equal("Public biography", root.GetProperty("bio").GetString());

        var recipes = root.GetProperty("recipes");
        Assert.Equal(2, recipes.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, recipes.GetProperty("page").GetInt32());
        Assert.Equal(1, recipes.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, recipes.GetProperty("totalPages").GetInt32());
        Assert.True(recipes.GetProperty("hasNextPage").GetBoolean());
        Assert.False(recipes.GetProperty("hasPreviousPage").GetBoolean());
        Assert.Equal(newestId.ToString(), recipes.GetProperty("items")[0].GetProperty("id").GetString());

        var pageTwoResponse = await factory.CreateIsolatedClient()
            .GetAsync($"/api/v1/authors/{authorId}?page=2&pageSize=1");
        using var pageTwoJson = JsonDocument.Parse(await pageTwoResponse.Content.ReadAsStringAsync());
        var pageTwo = pageTwoJson.RootElement.GetProperty("recipes");
        Assert.Equal(olderId.ToString(), pageTwo.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.False(pageTwo.GetProperty("hasNextPage").GetBoolean());
        Assert.True(pageTwo.GetProperty("hasPreviousPage").GetBoolean());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    public async Task GetAuthorProfile_MissingOrInactiveAuthor_ReturnsNotFound(string mode)
    {
        var authorId = mode == "missing" ? $"missing-{Guid.NewGuid():N}" : $"inactive-{Guid.NewGuid():N}";
        if (mode == "inactive")
            await SeedAsync(CreateAuthor(authorId, "Inactive", isActive: false));

        var response = await factory.CreateIsolatedClient().GetAsync($"/api/v1/authors/{authorId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    public async Task GetAuthorProfile_InvalidPagination_ReturnsBadRequest(string query)
    {
        var response = await factory.CreateIsolatedClient()
            .GetAsync($"/api/v1/authors/some-author?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task SeedAsync(params object[] entities)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    private static IdentityApplicationUser CreateAuthor(string id, string displayName, bool isActive = true) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        Email = $"{id}@example.test",
        NormalizedEmail = $"{id}@example.test".ToUpperInvariant(),
        DisplayName = displayName,
        AvatarUrl = "https://example.test/avatar.png",
        Bio = "Public biography",
        IsActive = isActive
    };
}
