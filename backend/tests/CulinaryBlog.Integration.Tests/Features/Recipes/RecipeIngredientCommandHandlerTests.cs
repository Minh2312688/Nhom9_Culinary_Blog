using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Features.Recipes;

public sealed class RecipeIngredientCommandHandlerTests
{
    [Fact]
    public void AddIngredientValidator_AllowsUnmeasuredIngredientButRequiresOrderIndex()
    {
        var validator = new AddRecipeIngredientCommandValidator();
        var valid = validator.Validate(new AddRecipeIngredientCommand(Guid.NewGuid(), "Salt", null, null, null, 0));
        var missingOrder = validator.Validate(new AddRecipeIngredientCommand(Guid.NewGuid(), "Salt", null, null, null, null));

        Assert.True(valid.IsValid);
        Assert.Contains(missingOrder.Errors, error => error.PropertyName == "OrderIndex");
    }

    [Fact]
    public void AddIngredientValidator_RejectsNonPositiveProvidedQuantity()
    {
        var validator = new AddRecipeIngredientCommandValidator();

        var result = validator.Validate(new AddRecipeIngredientCommand(Guid.NewGuid(), "Salt", 0, "tsp", null, 0));

        Assert.Contains(result.Errors, error => error.PropertyName == "Quantity");
    }

    [Fact]
    public async Task AddIngredient_PreservesNullableQuantityAndRequestedOrder_AndInvalidatesRecipeCache()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var cache = new RecordingRecipeCache();
        var handler = new AddRecipeIngredientCommandHandler(fixture.Db, fixture.User, cache);

        var result = await handler.Handle(new AddRecipeIngredientCommand(
            fixture.Recipe.Id, "Salt", null, null, "to taste", 3), CancellationToken.None);

        Assert.Equal("Salt", result.Name);
        Assert.Null(result.Quantity);
        Assert.Null(result.Unit);
        Assert.Equal("to taste", result.Notes);
        Assert.Equal(3, result.OrderIndex);
        Assert.Equal(1, await fixture.Db.RecipeIngredients.CountAsync());
        Assert.Contains("recipes:slug:vegetable-soup", cache.RemovedKeys);
    }

    [Fact]
    public async Task AddIngredient_RejectsAnotherAuthorsRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        fixture.User.Id = "different-user";
        var handler = new AddRecipeIngredientCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new AddRecipeIngredientCommand(fixture.Recipe.Id, "Salt", 1, "tsp", null, 0), CancellationToken.None));
    }

    [Fact]
    public async Task AddIngredient_AllowsAdminToManageAnotherAuthorsRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        fixture.User.IsAdmin = true;
        var handler = new AddRecipeIngredientCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        var result = await handler.Handle(new AddRecipeIngredientCommand(
            fixture.Recipe.Id, "Salt", 1, "tsp", null, 0), CancellationToken.None);

        Assert.Equal("Salt", result.Name);
        Assert.Equal(1, await fixture.Db.RecipeIngredients.CountAsync());
    }

    [Fact]
    public async Task UpdateIngredient_ReplacesAllFieldsIncludingClearingNullableValues()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var ingredient = new RecipeIngredient
        {
            RecipeId = fixture.Recipe.Id, Name = "Old", Quantity = 2, Unit = "cups", Notes = "old", OrderIndex = 0
        };
        fixture.Db.RecipeIngredients.Add(ingredient);
        await fixture.Db.SaveChangesAsync();
        var handler = new UpdateRecipeIngredientCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        var result = await handler.Handle(new UpdateRecipeIngredientCommand(
            fixture.Recipe.Id, ingredient.Id, "Flour", null, null, null, 4), CancellationToken.None);

        Assert.Equal("Flour", result.Name);
        Assert.Null(result.Quantity);
        Assert.Null(result.Unit);
        Assert.Null(result.Notes);
        Assert.Equal(4, result.OrderIndex);
    }

    [Fact]
    public async Task DeleteIngredient_SoftDeletesItAndHidesItFromNormalQueries()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var ingredient = new RecipeIngredient { RecipeId = fixture.Recipe.Id, Name = "Salt", OrderIndex = 0 };
        fixture.Db.RecipeIngredients.Add(ingredient);
        await fixture.Db.SaveChangesAsync();
        var handler = new DeleteRecipeIngredientCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await handler.Handle(new DeleteRecipeIngredientCommand(fixture.Recipe.Id, ingredient.Id), CancellationToken.None);

        Assert.Empty(await fixture.Db.RecipeIngredients.ToListAsync());
        Assert.True((await fixture.Db.RecipeIngredients.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task UpdateIngredient_RejectsIngredientThatDoesNotBelongToRouteRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var ingredient = new RecipeIngredient { RecipeId = fixture.Recipe.Id, Name = "Salt", OrderIndex = 0 };
        fixture.Db.RecipeIngredients.Add(ingredient);
        await fixture.Db.SaveChangesAsync();
        var handler = new UpdateRecipeIngredientCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UpdateRecipeIngredientCommand(
            Guid.NewGuid(), ingredient.Id, "Flour", 1, "cup", null, 1), CancellationToken.None));
    }

    private sealed class RecipeFixture : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required TestCurrentUser User { get; init; }
        public required Recipe Recipe { get; init; }

        public static async Task<RecipeFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeIngredient_{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options);
            var user = new TestCurrentUser { Id = "recipe-owner" };
            var identityUser = new IdentityApplicationUser { Id = user.Id, UserName = user.Id, DisplayName = "Recipe Owner" };
            var category = Category.Create("Soup", null);
            var recipe = new Recipe
            {
                Title = "Vegetable Soup", Slug = "vegetable-soup", CategoryId = category.Id,
                AuthorId = user.Id, Difficulty = "Easy", Servings = 2
            };
            db.Set<IdentityApplicationUser>().Add(identityUser);
            db.Categories.Add(category);
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return new RecipeFixture { Db = db, User = user, Recipe = recipe };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? Id { get; set; }
        public string? UserId => Id;
        public bool IsAdmin { get; set; }
        public bool IsAuthenticated => Id is not null || IsAdmin;
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public List<string> RemovedKeys { get; } = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { RemovedKeys.Add(key); return Task.CompletedTask; }
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
