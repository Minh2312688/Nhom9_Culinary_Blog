using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class GetRecipesVisibilityTests
{
    [Fact]
    public async Task AnonymousList_OnlyIncludesPublishedRecipes()
    {
        await using var fixture = await RecipeListFixture.CreateAsync();
        var result = await fixture.GetRecipesAsync(null, false);

        Assert.Equal(7, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(nameof(RecipeStatus.Published), item.Status.ToString()));
    }

    [Fact]
    public async Task AuthorList_IncludesPublishedAndOwnDraftsAndArchived()
    {
        await using var fixture = await RecipeListFixture.CreateAsync();
        var result = await fixture.GetRecipesAsync("author-one", false);

        Assert.Equal(9, result.TotalCount);
        Assert.Contains(result.Items, item => item.Title == "Owner draft");
        Assert.Contains(result.Items, item => item.Title == "Owner archived");
        Assert.DoesNotContain(result.Items, item => item.Title == "Other draft");
    }

    [Fact]
    public async Task AdminList_IncludesEveryRecipeStatus()
    {
        await using var fixture = await RecipeListFixture.CreateAsync();
        var result = await fixture.GetRecipesAsync(null, true);

        Assert.Equal(10, result.TotalCount);
    }

    [Fact]
    public async Task PunctuationOnlySearch_UsesFilteredListFallback()
    {
        await using var fixture = await RecipeListFixture.CreateAsync();
        var result = await fixture.GetRecipesAsync(null, false, new GetRecipesQuery(Search: "\" !! :* |"));

        Assert.Equal(7, result.TotalCount);
        fixture.VerifySearchWasNotCalled();
    }

    [Fact]
    public async Task ListFilters_RequireEveryPredicateToMatch()
    {
        await using var fixture = await RecipeListFixture.CreateAsync();
        var result = await fixture.GetRecipesAsync("author-one", false, new GetRecipesQuery(
            CategoryId: fixture.SoupCategoryId,
            Difficulty: "easy",
            MaxCookTime: 20,
            MinServings: 10));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("All filters match", Assert.Single(result.Items).Title);
    }

    private sealed class RecipeListFixture : IAsyncDisposable
    {
        private readonly ApplicationDbContext db;
        private readonly Mock<IRecipeRepository> repository;

        public Guid SoupCategoryId { get; }

        private RecipeListFixture(ApplicationDbContext db, Mock<IRecipeRepository> repository, Guid soupCategoryId)
        {
            this.db = db;
            this.repository = repository;
            SoupCategoryId = soupCategoryId;
        }

        public async Task<PaginatedResult<CulinaryBlog.Application.DTOs.Recipes.RecipeSummaryDto>> GetRecipesAsync(
            string? userId,
            bool isAdmin,
            GetRecipesQuery? query = null)
        {
            repository.SetupGet(value => value.Query).Returns(db.Recipes);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(value => value.UserId).Returns(userId);
            user.SetupGet(value => value.IsAdmin).Returns(isAdmin);
            var handler = new GetRecipesQueryHandler(repository.Object, user.Object);
            return await handler.Handle(query ?? new GetRecipesQuery(), CancellationToken.None);
        }

        public void VerifySearchWasNotCalled() => repository.Verify(value => value.SearchAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<Guid?>(), It.IsAny<RecipeDifficulty?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        public static async Task<RecipeListFixture> CreateAsync()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeVisibility_{Guid.NewGuid():N}").Options);
            var soup = Category.Create("Soup", null);
            var salad = Category.Create("Salad", null);
            const string firstAuthorId = "author-one";
            const string secondAuthorId = "author-two";
            db.Set<IdentityApplicationUser>().AddRange(
                new IdentityApplicationUser { Id = firstAuthorId, UserName = firstAuthorId, DisplayName = "Author One" },
                new IdentityApplicationUser { Id = secondAuthorId, UserName = secondAuthorId, DisplayName = "Author Two" });
            db.Categories.AddRange(soup, salad);
            db.Recipes.AddRange(
                CreateRecipe("Published one", firstAuthorId, soup.Id, RecipeStatus.Published),
                CreateRecipe("Published two", secondAuthorId, salad.Id, RecipeStatus.Published),
                CreateRecipe("Owner draft", firstAuthorId, soup.Id, RecipeStatus.Draft),
                CreateRecipe("Owner archived", firstAuthorId, soup.Id, RecipeStatus.Archived),
                CreateRecipe("Other draft", secondAuthorId, salad.Id, RecipeStatus.Draft),
                CreateRecipe("All filters match", firstAuthorId, soup.Id, RecipeStatus.Published,
                    RecipeDifficulty.Easy, 15, 10),
                CreateRecipe("Wrong category", firstAuthorId, salad.Id, RecipeStatus.Published,
                    RecipeDifficulty.Easy, 15, 4),
                CreateRecipe("Wrong difficulty", firstAuthorId, soup.Id, RecipeStatus.Published,
                    RecipeDifficulty.Hard, 15, 4),
                CreateRecipe("Too slow", firstAuthorId, soup.Id, RecipeStatus.Published,
                    RecipeDifficulty.Easy, 25, 4),
                CreateRecipe("Too few servings", firstAuthorId, soup.Id, RecipeStatus.Published,
                    RecipeDifficulty.Easy, 15, 2));
            await db.SaveChangesAsync();

            return new RecipeListFixture(db, new Mock<IRecipeRepository>(), soup.Id);
        }

        private static Recipe CreateRecipe(
            string title,
            string authorId,
            Guid categoryId,
            RecipeStatus status,
            RecipeDifficulty difficulty = RecipeDifficulty.Easy,
            int cookTime = 15,
            int servings = 4) => new()
        {
            Title = title,
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            AuthorId = authorId,
            CategoryId = categoryId,
            Difficulty = difficulty,
            Status = status,
            CookTimeMinutes = cookTime,
            Servings = servings
        };

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
