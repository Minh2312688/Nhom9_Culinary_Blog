using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class RecipeLifecycleTests
{
    [Fact]
    public async Task Publish_SetsAndPersistsTheCurrentUtcTimestamp()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var handler = fixture.CreateHandler();
        var beforePublish = DateTimeOffset.UtcNow;

        await handler.Handle(new PublishRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        var afterPublish = DateTimeOffset.UtcNow;
        var persisted = await fixture.Db.Recipes.SingleAsync(recipe => recipe.Id == fixture.Recipe.Id);
        Assert.Equal(RecipeStatus.Published, persisted.Status);
        Assert.InRange(persisted.PublishedAt!.Value, beforePublish, afterPublish);
        Assert.Equal(TimeSpan.Zero, persisted.PublishedAt.Value.Offset);
    }

    [Fact]
    public async Task Archive_PreservesThePublicationTimestamp()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
        await using var fixture = await RecipeLifecycleFixture.CreateAsync(RecipeStatus.Published, publishedAt);

        await fixture.CreateHandler().Handle(
            new ArchiveRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        var persisted = await fixture.Db.Recipes.SingleAsync(recipe => recipe.Id == fixture.Recipe.Id);
        Assert.Equal(RecipeStatus.Archived, persisted.Status);
        Assert.Equal(publishedAt, persisted.PublishedAt);
    }

    private sealed class RecipeLifecycleFixture : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required TestCurrentUser User { get; init; }
        public required Recipe Recipe { get; init; }

        public static async Task<RecipeLifecycleFixture> CreateAsync(
            RecipeStatus status = RecipeStatus.Draft,
            DateTimeOffset? publishedAt = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeLifecycle_{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options);
            var user = new TestCurrentUser();
            var recipe = new Recipe
            {
                Title = "Vegetable Soup",
                Slug = "vegetable-soup",
                AuthorId = user.UserId!,
                Status = status,
                PublishedAt = publishedAt
            };
            recipe.Ingredients.Add(new RecipeIngredient { Name = "Flour" });
            recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return new RecipeLifecycleFixture { Db = db, User = user, Recipe = recipe };
        }

        public RecipeLifecycleHandler CreateHandler() =>
            new(Db, User, new RecordingRecipeCache());

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId { get; } = "recipe-owner";
        public bool IsAdmin => false;
        public bool IsAuthenticated => true;
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(T));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
