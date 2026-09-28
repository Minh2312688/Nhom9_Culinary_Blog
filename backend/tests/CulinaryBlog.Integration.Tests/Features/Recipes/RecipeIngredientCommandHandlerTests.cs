using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Features.Recipes;

public sealed class RecipeIngredientCommandHandlerTests
{
    [Fact]
    public async Task AddIngredient_TrimsOptionalTextAndInvalidatesRecipeCache()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var cache = new RecordingRecipeCache();
        var handler = new AddRecipeIngredientCommandHandler(fixture.Db, fixture.User, cache);

        var result = await handler.Handle(new AddRecipeIngredientCommand(
            fixture.Recipe.Id, "  Salt  ", null, "  ", "  to taste  ", 2), CancellationToken.None);

        Assert.Equal("Salt", result.Name);
        Assert.Null(result.Quantity);
        Assert.Null(result.Unit);
        Assert.Equal("to taste", result.Notes);
        Assert.Equal(2, result.OrderIndex);
        Assert.Contains("recipes:slug:vegetable-soup", cache.RemovedKeys);
        Assert.Contains("recipes:", cache.RemovedPrefixes);
    }

    [Fact]
    public async Task UpdateIngredient_ChangesFieldsAndRejectsIngredientFromAnotherRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var ingredient = new RecipeIngredient { Name = "Salt", Quantity = 1, Unit = "g", OrderIndex = 0 };
        ingredient.RecipeId = fixture.Recipe.Id;
        fixture.Db.RecipeIngredients.Add(ingredient);
        await fixture.Db.SaveChangesAsync();
        var cache = new RecordingRecipeCache();
        var handler = new UpdateRecipeIngredientCommandHandler(fixture.Db, fixture.User, cache);

        var result = await handler.Handle(new UpdateRecipeIngredientCommand(
            fixture.Recipe.Id, ingredient.Id, " Pepper ", 2.5m, " tsp ", null, 1), CancellationToken.None);

        Assert.Equal("Pepper", result.Name);
        Assert.Equal(2.5m, result.Quantity);
        Assert.Equal("tsp", result.Unit);
        Assert.Null(result.Notes);
        Assert.Equal(1, result.OrderIndex);
        Assert.Contains("recipes:slug:vegetable-soup", cache.RemovedKeys);

        var otherRecipe = new Recipe
        {
            Title = "Other Soup", Slug = "other-soup", CategoryId = fixture.Recipe.CategoryId,
            AuthorId = fixture.User.Id!, Difficulty = RecipeDifficulty.Easy, Servings = 1
        };
        fixture.Db.Recipes.Add(otherRecipe);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UpdateRecipeIngredientCommand(
            otherRecipe.Id, ingredient.Id, "Other", null, null, null, 0), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteIngredient_SoftDeletesAndInvalidatesRecipeCache()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var ingredient = new RecipeIngredient { Name = "Salt", OrderIndex = 0 };
        ingredient.RecipeId = fixture.Recipe.Id;
        fixture.Db.RecipeIngredients.Add(ingredient);
        await fixture.Db.SaveChangesAsync();
        var cache = new RecordingRecipeCache();
        var handler = new DeleteRecipeIngredientCommandHandler(fixture.Db, fixture.User, cache);

        await handler.Handle(new DeleteRecipeIngredientCommand(fixture.Recipe.Id, ingredient.Id), CancellationToken.None);

        Assert.Empty(await fixture.Db.RecipeIngredients.Where(x => x.Id == ingredient.Id).ToListAsync());
        Assert.True((await fixture.Db.RecipeIngredients.IgnoreQueryFilters().SingleAsync(x => x.Id == ingredient.Id)).IsDeleted);
        Assert.Contains("recipes:slug:vegetable-soup", cache.RemovedKeys);
        Assert.Contains("recipes:", cache.RemovedPrefixes);
    }

    [Fact]
    public async Task AddIngredient_RejectsAnotherAuthorsRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        fixture.User.Id = "another-user";
        var handler = new AddRecipeIngredientCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new AddRecipeIngredientCommand(
            fixture.Recipe.Id, "Salt", null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task IngredientValidators_EnforceNameQuantityAndOrderIndex()
    {
        var addValidator = new AddRecipeIngredientCommandValidator();
        var updateValidator = new UpdateRecipeIngredientCommandValidator();

        var addErrors = addValidator.Validate(new AddRecipeIngredientCommand(Guid.NewGuid(), " ", 0, null, null, -1));
        var updateErrors = updateValidator.Validate(new UpdateRecipeIngredientCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('x', 201), -1, new string('u', 51), new string('n', 501), -1));

        Assert.Contains(addErrors.Errors, error => error.PropertyName == "Name");
        Assert.Contains(addErrors.Errors, error => error.PropertyName == "Quantity");
        Assert.Contains(addErrors.Errors, error => error.PropertyName == "OrderIndex");
        Assert.Contains(updateErrors.Errors, error => error.PropertyName == "Name");
        Assert.Contains(updateErrors.Errors, error => error.PropertyName == "Unit");
        Assert.Contains(updateErrors.Errors, error => error.PropertyName == "Notes");
        Assert.Contains(updateErrors.Errors, error => error.PropertyName == "OrderIndex");
    }

    private sealed class RecipeFixture : IAsyncDisposable
    {
        public required AuthDbContext Db { get; init; }
        public required TestCurrentUser User { get; init; }
        public required Recipe Recipe { get; init; }

        public static async Task<RecipeFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase($"RecipeIngredients_{Guid.NewGuid():N}").Options;
            var db = new AuthDbContext(options);
            var user = new TestCurrentUser { Id = "recipe-owner" };
            var category = Category.Create("Soup", null);
            var recipe = new Recipe
            {
                Title = "Vegetable Soup", Slug = "vegetable-soup", CategoryId = category.Id,
                AuthorId = user.Id, Difficulty = RecipeDifficulty.Easy, Servings = 2
            };
            db.Set<IdentityApplicationUser>().Add(new IdentityApplicationUser
                { Id = user.Id, UserName = user.Id, DisplayName = "Recipe Owner" });
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
        public List<string> RemovedPrefixes { get; } = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { RemovedKeys.Add(key); return Task.CompletedTask; }
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) { RemovedPrefixes.Add(prefix); return Task.CompletedTask; }
    }
}
