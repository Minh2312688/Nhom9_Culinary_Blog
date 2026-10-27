using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class GetRecipeBySlugQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPublicationDateAndActiveAuthorWithoutCachingIdentityFields()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var recipe = CreatePublishedRecipe(publishedAt);
        await using var db = await CreateDbAsync(recipe);
        var cache = new RecordingRecipeCache();
        var identity = CreateIdentityService(active: true);
        var repository = CreateRecipeRepository(db);
        var handler = new GetRecipeBySlugQueryHandler(
            repository.Object,
            new Mock<ICurrentUserService>().Object,
            cache,
            identity.Object);

        var result = await handler.Handle(new GetRecipeBySlugQuery(recipe.Slug), CancellationToken.None);

        Assert.Equal(publishedAt, result.PublishedAt);
        Assert.NotNull(result.Author);
        Assert.Equal("author-1", result.Author.Id);
        Assert.Equal("Public Author", result.Author.DisplayName);
        Assert.Equal("https://example.test/avatar.png", result.Author.AvatarUrl);
        Assert.Null(cache.Value!.Author);
        identity.Verify(service => service.GetUserProfileByIdAsync("author-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CacheHit_ResolvesActiveAuthorAgain()
    {
        var recipe = CreatePublishedRecipe(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        await using var db = await CreateDbAsync(recipe);
        var cachedRecipe = new RecipeDetailDto(
            recipe.Id, recipe.Title, recipe.Slug, recipe.Description, recipe.PrepTimeMinutes,
            recipe.CookTimeMinutes, recipe.Servings, recipe.Difficulty.ToString(), recipe.Status,
            recipe.CategoryId, recipe.AuthorId, recipe.PublishedAt, recipe.RowVersion,
            [], [], [], null, new RecipeAuthorDto("cached-author", "Stale cached display name", null));
        var cache = new RecordingRecipeCache { Value = cachedRecipe };
        var identity = CreateIdentityService(active: true);
        var repository = CreateRecipeRepository(db);
        var handler = new GetRecipeBySlugQueryHandler(
            repository.Object,
            new Mock<ICurrentUserService>().Object,
            cache,
            identity.Object);

        var result = await handler.Handle(new GetRecipeBySlugQuery(recipe.Slug), CancellationToken.None);

        Assert.Equal("Public Author", result.Author!.DisplayName);
        repository.VerifyGet(value => value.Query, Times.Never);
        identity.Verify(service => service.GetUserProfileByIdAsync("author-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveAuthor_ReturnsNoPublicAuthor()
    {
        var recipe = CreatePublishedRecipe(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        await using var db = await CreateDbAsync(recipe);
        var identity = CreateIdentityService(active: false);
        var repository = CreateRecipeRepository(db);
        var handler = new GetRecipeBySlugQueryHandler(
            repository.Object,
            new Mock<ICurrentUserService>().Object,
            new RecordingRecipeCache(),
            identity.Object);

        var result = await handler.Handle(new GetRecipeBySlugQuery(recipe.Slug), CancellationToken.None);

        Assert.Null(result.Author);
    }

    private static Mock<IIdentityService> CreateIdentityService(bool active)
    {
        var identity = new Mock<IIdentityService>();
        identity.Setup(service => service.GetUserProfileByIdAsync("author-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                true, "author-1", "private@example.com", "Public Author", "https://example.test/avatar.png",
                "Private to the profile endpoint", true, DateTimeOffset.UtcNow, active, false, ["Admin"]));
        return identity;
    }

    private static Recipe CreatePublishedRecipe(DateTimeOffset publishedAt) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Published Recipe",
        Slug = "published-recipe",
        AuthorId = "author-1",
        CategoryId = Guid.NewGuid(),
        Status = RecipeStatus.Published,
        PublishedAt = publishedAt
    };

    private static async Task<ApplicationDbContext> CreateDbAsync(Recipe recipe)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"RecipeRead_{Guid.NewGuid():N}").Options);
        var category = Category.Create("Recipe Read Test");
        recipe.CategoryId = category.Id;
        db.Users.Add(new IdentityApplicationUser
        {
            Id = "author-1",
            UserName = "author-1",
            NormalizedUserName = "AUTHOR-1",
            DisplayName = "Public Author"
        });
        db.Categories.Add(category);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return db;
    }

    private static Mock<IRecipeRepository> CreateRecipeRepository(ApplicationDbContext db)
    {
        var repository = new Mock<IRecipeRepository>();
        repository.Setup(value => value.Query).Returns(db.Recipes);
        return repository;
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public RecipeDetailDto? Value { get; set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Value is null ? default : (T?)(object)Value);

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            Value = value as RecipeDetailDto;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
