using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Features.Recipes;

public sealed class RecipeStepCommandHandlerTests
{
    [Fact]
    public async Task AddStep_AssignsMaxExistingStepNumberPlusOne_AndInvalidatesRecipeCache()
    {
        await using var fixture = await RecipeFixture.CreateAsync(1, 4);
        var cache = new RecordingRecipeCache();
        var handler = new AddRecipeStepCommandHandler(fixture.Db, fixture.User, cache);

        var result = await handler.Handle(new AddRecipeStepCommand(
            fixture.Recipe.Id, "Serve", "Serve while hot.", 2, null), CancellationToken.None);

        Assert.Equal(5, result.StepNumber);
        Assert.Equal("Serve", result.Title);
        Assert.Equal(2, result.DurationMinutes);
        Assert.Contains("recipes:slug:vegetable-soup", cache.RemovedKeys);
    }

    [Fact]
    public async Task ApplicationDbContext_DeleteRecipe_SoftDeletesIt()
    {
        await using var fixture = await RecipeFixture.CreateAsync();

        fixture.Db.Recipes.Remove(fixture.Recipe);
        await fixture.Db.SaveChangesAsync();

        var deleted = await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync(x => x.Id == fixture.Recipe.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Empty(await fixture.Db.Recipes.Where(x => x.Id == fixture.Recipe.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdateRecipe_InvalidatesDetailCacheForOldAndNewSlugs()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        var cache = new RecordingRecipeCache();
        var handler = new UpdateRecipeCommandHandler(fixture.Db, fixture.User, cache);
        var command = new UpdateRecipeCommand(
            fixture.Recipe.Id, "New Soup Name", null, fixture.Recipe.CategoryId,
            5, 10, 2, "Easy", fixture.Recipe.RowVersion ?? []);

        await handler.Handle(command, CancellationToken.None);

        Assert.Contains("recipes:slug:vegetable-soup", cache.RemovedKeys);
        Assert.Contains("recipes:slug:new-soup-name", cache.RemovedKeys);
    }

    [Fact]
    public async Task UpdateStep_ChangesStepContentButKeepsServerAssignedNumber()
    {
        await using var fixture = await RecipeFixture.CreateAsync(1, 2);
        var step = await fixture.Db.RecipeSteps.SingleAsync(x => x.RecipeId == fixture.Recipe.Id && x.StepNumber == 2);
        var handler = new UpdateRecipeStepCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        var result = await handler.Handle(new UpdateRecipeStepCommand(
            fixture.Recipe.Id, step.Id, "Finish", "Finish the dish.", null, null), CancellationToken.None);

        Assert.Equal(2, result.StepNumber);
        Assert.Equal("Finish", result.Title);
        Assert.Null(result.DurationMinutes);
        Assert.Null(result.ImageUrl);
    }

    [Fact]
    public async Task DeleteStep_SoftDeletesTargetAndRenumbersRemainingActiveSteps()
    {
        await using var fixture = await RecipeFixture.CreateAsync(1, 2, 3);
        var stepToDelete = await fixture.Db.RecipeSteps.SingleAsync(x => x.RecipeId == fixture.Recipe.Id && x.StepNumber == 2);
        var handler = new DeleteRecipeStepCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await handler.Handle(new DeleteRecipeStepCommand(fixture.Recipe.Id, stepToDelete.Id), CancellationToken.None);

        var remaining = await fixture.Db.RecipeSteps.Where(x => x.RecipeId == fixture.Recipe.Id)
            .OrderBy(x => x.StepNumber).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, remaining.Select(x => x.StepNumber));
        Assert.Equal("Step 3", remaining[1].Title);
        var deleted = await fixture.Db.RecipeSteps.IgnoreQueryFilters().SingleAsync(x => x.Id == stepToDelete.Id);
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteStep_ParksExistingDeletedRowsWithoutReusingTheirNumbers()
    {
        await using var fixture = await RecipeFixture.CreateAsync(1, 2);
        fixture.Db.RecipeSteps.Add(new RecipeStep
        {
            RecipeId = fixture.Recipe.Id, StepNumber = -1, IsDeleted = true,
            Title = "Old deleted step", Description = "Previously deleted"
        });
        await fixture.Db.SaveChangesAsync();
        var target = await fixture.Db.RecipeSteps.SingleAsync(x => x.StepNumber == 2);
        var handler = new DeleteRecipeStepCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await handler.Handle(new DeleteRecipeStepCommand(fixture.Recipe.Id, target.Id), CancellationToken.None);

        var deletedNumbers = await fixture.Db.RecipeSteps.IgnoreQueryFilters()
            .Where(x => x.RecipeId == fixture.Recipe.Id && x.IsDeleted)
            .Select(x => x.StepNumber).ToListAsync();
        Assert.Equal(2, deletedNumbers.Distinct().Count());
        Assert.Equal(new[] { 1 }, await fixture.Db.RecipeSteps.Where(x => x.RecipeId == fixture.Recipe.Id)
            .Select(x => x.StepNumber).ToArrayAsync());
    }

    [Fact]
    public async Task AddStep_RejectsAnotherAuthorsRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync();
        fixture.User.Id = "other-user";
        var handler = new AddRecipeStepCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new AddRecipeStepCommand(
            fixture.Recipe.Id, "Heat", "Heat the pan.", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStep_ReturnsNotFoundWhenStepBelongsToDifferentRecipe()
    {
        await using var fixture = await RecipeFixture.CreateAsync(1);
        var step = await fixture.Db.RecipeSteps.SingleAsync(x => x.RecipeId == fixture.Recipe.Id);
        var handler = new UpdateRecipeStepCommandHandler(fixture.Db, fixture.User, new RecordingRecipeCache());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UpdateRecipeStepCommand(
            Guid.NewGuid(), step.Id, "Other", "Other recipe step.", null, null), CancellationToken.None));
    }

    [Fact]
    public void AddStepValidator_RequiresTitleAndDescriptionAndRejectsNegativeDuration()
    {
        var validator = new AddRecipeStepCommandValidator();

        var missingContent = validator.Validate(new AddRecipeStepCommand(Guid.NewGuid(), " ", " ", null, null));
        var negativeDuration = validator.Validate(new AddRecipeStepCommand(Guid.NewGuid(), "Heat", "Heat the pan.", -1, null));

        Assert.Contains(missingContent.Errors, error => error.PropertyName == "Title");
        Assert.Contains(missingContent.Errors, error => error.PropertyName == "Description");
        Assert.Contains(negativeDuration.Errors, error => error.PropertyName == "DurationMinutes");
    }

    private sealed class RecipeFixture : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required TestCurrentUser User { get; init; }
        public required Recipe Recipe { get; init; }

        public static async Task<RecipeFixture> CreateAsync(params int[] stepNumbers)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeSteps_{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options);
            var user = new TestCurrentUser { Id = "recipe-owner" };
            var category = Category.Create("Soup", null);
            var recipe = new Recipe
            {
                Title = "Vegetable Soup", Slug = "vegetable-soup", CategoryId = category.Id,
                AuthorId = user.Id, Difficulty = "Easy", Servings = 2
            };
            db.Set<IdentityApplicationUser>().Add(new IdentityApplicationUser
                { Id = user.Id, UserName = user.Id, DisplayName = "Recipe Owner" });
            db.Categories.Add(category);
            db.Recipes.Add(recipe);
            foreach (var number in stepNumbers)
                recipe.Steps.Add(new RecipeStep
                    { StepNumber = number, Title = $"Step {number}", Description = $"Description {number}" });
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
