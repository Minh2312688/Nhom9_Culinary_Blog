using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class AdminRecipeMutationTests
{
    [Fact]
    public async Task CreateRecipe_AdminIsForbidden()
    {
        await using var db = CreateDb();
        var category = Category.Create("Soup");
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var user = CreateAdminUser();
        var repositories = CreateRepositories(db);
        var handler = new CreateRecipeCommandHandler(
            repositories.Recipes.Object,
            repositories.Categories.Object,
            Mock.Of<IUnitOfWork>(),
            user.Object,
            Mock.Of<IRecipeCache>());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new CreateRecipeCommand("Soup", null, category.Id, 10, 20, 2, "Easy"),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRecipe_AdminIsForbiddenEvenForAnotherAuthor()
    {
        await using var db = CreateDb();
        var category = Category.Create("Soup");
        var recipe = new Recipe
        {
            Title = "Soup",
            Slug = "soup",
            AuthorId = "author",
            CategoryId = category.Id
        };
        db.Categories.Add(category);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var handler = new UpdateRecipeCommandHandler(db, CreateAdminUser().Object, Mock.Of<IRecipeCache>());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new UpdateRecipeCommand(recipe.Id, "Changed", null, category.Id, 10, 20, 2, "Easy", recipe.RowVersion),
            CancellationToken.None));
    }

    [Fact]
    public async Task AddRecipeIngredient_AdminIsForbidden()
    {
        await using var db = CreateDb();
        var recipe = await AddRecipeAsync(db);
        var handler = new AddRecipeIngredientCommandHandler(
            db, CreateAdminUser().Object, Mock.Of<IRecipeCache>());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new AddRecipeIngredientCommand(recipe.Id, "Flour", 1, "cup", null, 0),
            CancellationToken.None));
    }

    [Fact]
    public async Task AddRecipeStep_AdminIsForbidden()
    {
        await using var db = CreateDb();
        var recipe = await AddRecipeAsync(db);
        var handler = new AddRecipeStepCommandHandler(
            db, CreateAdminUser().Object, Mock.Of<IRecipeCache>());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new AddRecipeStepCommand(recipe.Id, "Mix", "Mix ingredients.", null, null),
            CancellationToken.None));
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AdminRecipeMutation_{Guid.NewGuid():N}").Options);

    private static async Task<Recipe> AddRecipeAsync(ApplicationDbContext db)
    {
        var category = Category.Create("Soup");
        var recipe = new Recipe
        {
            Title = "Soup",
            Slug = "soup",
            AuthorId = "author",
            CategoryId = category.Id
        };
        db.Categories.Add(category);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe;
    }

    private static Mock<ICurrentUserService> CreateAdminUser()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(current => current.IsAdmin).Returns(true);
        user.SetupGet(current => current.IsAuthenticated).Returns(true);
        user.SetupGet(current => current.UserId).Returns("administrator");
        return user;
    }

    private static (Mock<IRecipeRepository> Recipes, Mock<ICategoryRepository> Categories) CreateRepositories(
        ApplicationDbContext db)
    {
        var recipes = new Mock<IRecipeRepository>();
        recipes.SetupGet(repository => repository.Query).Returns(db.Recipes);
        recipes.Setup(repository => repository.Add(It.IsAny<Recipe>()))
            .Callback<Recipe>(recipe => db.Recipes.Add(recipe));
        var categories = new Mock<ICategoryRepository>();
        categories.SetupGet(repository => repository.Query).Returns(db.Categories);
        return (recipes, categories);
    }
}
