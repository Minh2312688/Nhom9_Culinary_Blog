using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Features.Authors;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Authors;

public sealed class GetAuthorProfileTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyTheActiveAuthorsPublishedRecipesInPublicationOrder()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AuthorProfile_{Guid.NewGuid():N}").Options);
        var category = Category.Create("Test", "Test category");
        var publishedNewest = CreateRecipe("newest", "author-1", category.Id, RecipeStatus.Published,
            new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var publishedTieFirst = CreateRecipe("tie-first", "author-1", category.Id, RecipeStatus.Published,
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        publishedTieFirst.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var publishedTieSecond = CreateRecipe("tie-second", "author-1", category.Id, RecipeStatus.Published,
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        publishedTieSecond.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var publishedOlder = CreateRecipe("older", "author-1", category.Id, RecipeStatus.Published,
            new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
        var historical = CreateRecipe("historical", "author-1", category.Id, RecipeStatus.Published, null);
        db.Categories.Add(category);
        db.Recipes.AddRange(
            historical,
            publishedOlder,
            publishedTieSecond,
            CreateRecipe("draft", "author-1", category.Id, RecipeStatus.Draft, null),
            CreateRecipe("archived", "author-1", category.Id, RecipeStatus.Archived,
                new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero)),
            CreateRecipe("other-author", "author-2", category.Id, RecipeStatus.Published,
                new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero)),
            publishedTieFirst,
            publishedNewest);
        await db.SaveChangesAsync();

        var identity = new Mock<IIdentityService>();
        identity.Setup(service => service.GetUserProfileByIdAsync("author-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                true, "author-1", "private@example.com", "Public Author", "https://example.test/avatar.png",
                "Public biography", true, DateTimeOffset.UtcNow, true, false, ["Admin"]));
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.Query).Returns(db.Recipes);
        var handler = new GetAuthorProfileQueryHandler(identity.Object, recipes.Object);

        var firstPage = await handler.Handle(new GetAuthorProfileQuery("author-1", Page: 1, PageSize: 2), CancellationToken.None);
        var secondPage = await handler.Handle(new GetAuthorProfileQuery("author-1", Page: 2, PageSize: 2), CancellationToken.None);
        var thirdPage = await handler.Handle(new GetAuthorProfileQuery("author-1", Page: 3, PageSize: 2), CancellationToken.None);

        Assert.Equal("author-1", firstPage.Id);
        Assert.Equal("Public Author", firstPage.DisplayName);
        Assert.Equal("https://example.test/avatar.png", firstPage.AvatarUrl);
        Assert.Equal("Public biography", firstPage.Bio);
        Assert.Equal(5, firstPage.Recipes.TotalCount);
        Assert.Equal(1, firstPage.Recipes.Page);
        Assert.Equal(2, firstPage.Recipes.PageSize);
        Assert.True(firstPage.Recipes.HasNextPage);
        Assert.False(firstPage.Recipes.HasPreviousPage);
        Assert.Equal(new[] { publishedNewest.Id, publishedTieFirst.Id }, firstPage.Recipes.Items.Select(x => x.Id));
        Assert.Equal(publishedNewest.PublishedAt, firstPage.Recipes.Items[0].PublishedAt);
        Assert.Equal(2, secondPage.Recipes.Page);
        Assert.True(secondPage.Recipes.HasNextPage);
        Assert.True(secondPage.Recipes.HasPreviousPage);
        Assert.Equal(new[] { publishedTieSecond.Id, publishedOlder.Id }, secondPage.Recipes.Items.Select(x => x.Id));
        Assert.Equal(3, thirdPage.Recipes.Page);
        Assert.False(thirdPage.Recipes.HasNextPage);
        Assert.True(thirdPage.Recipes.HasPreviousPage);
        Assert.Equal(new[] { historical.Id }, thirdPage.Recipes.Items.Select(x => x.Id));
        Assert.Null(thirdPage.Recipes.Items[0].PublishedAt);
        Assert.All(firstPage.Recipes.Items.Concat(secondPage.Recipes.Items).Concat(thirdPage.Recipes.Items),
            item => Assert.Equal(RecipeStatus.Published, item.Status));
    }

    [Fact]
    public async Task Handle_InactiveAuthor_ThrowsNotFound()
    {
        var identity = new Mock<IIdentityService>();
        identity.Setup(service => service.GetUserProfileByIdAsync("inactive", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileResult(
                true, "inactive", "private@example.com", "Inactive", null, null,
                true, DateTimeOffset.UtcNow, false, false, []));
        var recipes = new Mock<IRecipeRepository>();
        var handler = new GetAuthorProfileQueryHandler(identity.Object, recipes.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAuthorProfileQuery("inactive"), CancellationToken.None));

        recipes.VerifyGet(repository => repository.Query, Times.Never);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Validator_RejectsInvalidPagination(int page, int pageSize)
    {
        var result = new GetAuthorProfileQueryValidator().Validate(
            new GetAuthorProfileQuery("author-1", page, pageSize));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_AcceptsDefaultPagination()
    {
        var result = new GetAuthorProfileQueryValidator().Validate(new GetAuthorProfileQuery("author-1"));

        Assert.True(result.IsValid);
    }

    private static Recipe CreateRecipe(
        string slug,
        string authorId,
        Guid categoryId,
        RecipeStatus status,
        DateTimeOffset? publishedAt) => new()
    {
        Title = slug,
        Slug = slug,
        CategoryId = categoryId,
        AuthorId = authorId,
        Status = status,
        PublishedAt = publishedAt
    };
}
