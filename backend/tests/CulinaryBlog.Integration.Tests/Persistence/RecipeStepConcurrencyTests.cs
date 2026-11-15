using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Persistence;

public sealed class RecipeStepConcurrencyTests : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public async Task InitializeAsync()
    {
        if (DockerPostgresEnvironment.IsDockerAvailable)
        {
            container = DockerPostgresEnvironment.CreatePostgresBuilder().Build();
            await container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [DockerPostgresFact]
    public async Task ConcurrentStepModifications_OnPostgreSql_ThrowsDbUpdateConcurrencyException()
    {
        container.Should().NotBeNull();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container!.GetConnectionString())
            .Options;

        // 1. Migrate và khởi tạo dữ liệu
        await using (var setupDb = new ApplicationDbContext(options))
        {
            await setupDb.Database.MigrateAsync();
            var author = new ApplicationUser
            {
                Id = "baker-1",
                UserName = "baker1",
                Email = "baker1@example.com",
                DisplayName = "Baker 1"
            };
            setupDb.Users.Add(author);
            var category = Category.Create("Desserts", null);
            var recipe = new Recipe
            {
                Title = "Chocolate Cake",
                Slug = "chocolate-cake",
                CategoryId = category.Id,
                AuthorId = author.Id,
                Difficulty = RecipeDifficulty.Medium,
                Servings = 8
            };
            var step = new RecipeStep
            {
                RecipeId = recipe.Id,
                StepNumber = 1,
                Title = "Preheat oven",
                Description = "Preheat to 350F",
                DurationMinutes = 10
            };
            recipe.Steps.Add(step);
            setupDb.Categories.Add(category);
            setupDb.Recipes.Add(recipe);
            await setupDb.SaveChangesAsync();
        }

        // 2. Hai context độc lập tải cùng 1 step
        await using var contextA = new ApplicationDbContext(options);
        await using var contextB = new ApplicationDbContext(options);

        var stepA = await contextA.RecipeSteps.FirstAsync(s => s.Title == "Preheat oven");
        var stepB = await contextB.RecipeSteps.FirstAsync(s => s.Title == "Preheat oven");

        // 3. Context A cập nhật thành công trước
        stepA.Description = "Preheat oven to 375F (updated by A)";
        await contextA.SaveChangesAsync();
        await contextA.Database.ExecuteSqlRawAsync(
            "UPDATE \"RecipeSteps\" SET \"RowVersion\" = decode('01020304', 'hex') WHERE \"Id\" = {0}", stepA.Id);

        // 4. Context B cập nhật dữ liệu dựa trên row version cũ -> phải ném DbUpdateConcurrencyException
        stepB.Description = "Preheat oven to 325F (conflicting update by B)";
        var action = async () => await contextB.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerPostgresFact]
    public async Task ConcurrentRecipeUpdates_RotateVersionAndRejectStaleWrite()
    {
        container.Should().NotBeNull();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container!.GetConnectionString())
            .Options;
        var recipeId = Guid.NewGuid();

        await using (var setupDb = new ApplicationDbContext(options))
        {
            await setupDb.Database.MigrateAsync();
            setupDb.Users.Add(new ApplicationUser
            {
                Id = "recipe-concurrency-author",
                UserName = "recipe-concurrency-author",
                Email = "recipe-concurrency@example.com",
                DisplayName = "Recipe Concurrency Author"
            });
            var category = Category.Create("Concurrency Test");
            setupDb.Categories.Add(category);
            setupDb.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Concurrency Test Recipe",
                Slug = "concurrency-test-recipe",
                CategoryId = category.Id,
                AuthorId = "recipe-concurrency-author",
                Difficulty = RecipeDifficulty.Easy,
                Servings = 2
            });
            await setupDb.SaveChangesAsync();
        }

        await using var contextA = new ApplicationDbContext(options);
        await using var contextB = new ApplicationDbContext(options);
        var recipeA = await contextA.Recipes.SingleAsync(recipe => recipe.Id == recipeId);
        var recipeB = await contextB.Recipes.SingleAsync(recipe => recipe.Id == recipeId);
        var originalVersion = recipeB.RowVersion.ToArray();

        contextA.SetOriginalRowVersion(recipeA, originalVersion);
        recipeA.Title = "Updated Concurrency Test Recipe";
        await contextA.SaveChangesAsync();
        recipeA.RowVersion.Should().NotBeEquivalentTo(originalVersion);

        recipeB.Title = "Stale Concurrency Test Recipe";
        contextB.SetOriginalRowVersion(recipeB, originalVersion);
        var action = async () => await contextB.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerPostgresFact]
    public async Task ReloadAfterConcurrentRecipeUpdate_AllowsSavingIngredientsAndSteps()
    {
        container.Should().NotBeNull();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container!.GetConnectionString())
            .Options;
        var recipeId = Guid.NewGuid();
        Guid categoryId;

        await using (var setupDb = new ApplicationDbContext(options))
        {
            await setupDb.Database.MigrateAsync();
            setupDb.Users.Add(new ApplicationUser
            {
                Id = "recipe-collection-author",
                UserName = "recipe-collection-author",
                Email = "recipe-collection@example.com",
                DisplayName = "Recipe Collection Author"
            });
            var category = Category.Create("Collection Test");
            categoryId = category.Id;
            setupDb.Categories.Add(category);
            setupDb.Recipes.Add(new Recipe
            {
                Id = recipeId,
                Title = "Collection Test Recipe",
                Slug = "collection-test-recipe",
                CategoryId = category.Id,
                AuthorId = "recipe-collection-author",
                Difficulty = RecipeDifficulty.Easy,
                Servings = 2,
                Ingredients = [new RecipeIngredient
                {
                    Name = "Tomato",
                    Quantity = 1,
                    Unit = "piece",
                    OrderIndex = 0
                }],
                Steps = [new RecipeStep
                {
                    StepNumber = 1,
                    Title = "Prepare",
                    Description = "Prepare the tomato."
                }]
            });
            await setupDb.SaveChangesAsync();
        }

        byte[] staleVersion;
        await using (var staleContext = new ApplicationDbContext(options))
        {
            staleVersion = (await staleContext.Recipes.SingleAsync(recipe => recipe.Id == recipeId))
                .RowVersion.ToArray();
        }

        await using (var concurrentContext = new ApplicationDbContext(options))
        {
            var recipe = await concurrentContext.Recipes.SingleAsync(item => item.Id == recipeId);
            recipe.Title = "Changed by another session";
            await concurrentContext.SaveChangesAsync();
        }

        await using (var staleContext = new ApplicationDbContext(options))
        {
            var user = new TestCurrentUser("recipe-collection-author");
            var handler = new UpdateRecipeCommandHandler(staleContext, user, new NoOpRecipeCache());
            var staleAction = () => handler.Handle(
                CreateUpdateCommand(recipeId, categoryId, staleVersion, "My edit"),
                CancellationToken.None);

            await staleAction.Should().ThrowAsync<ConcurrencyException>();
        }

        await using (var retryContext = new ApplicationDbContext(options))
        {
            var currentRecipe = await retryContext.Recipes.SingleAsync(recipe => recipe.Id == recipeId);
            var user = new TestCurrentUser("recipe-collection-author");
            var handler = new UpdateRecipeCommandHandler(retryContext, user, new NoOpRecipeCache());

            var result = await handler.Handle(
                CreateUpdateCommand(recipeId, categoryId, currentRecipe.RowVersion, "My edit"),
                CancellationToken.None);

            result.Title.Should().Be("My edit");
            (await retryContext.RecipeIngredients.CountAsync(ingredient =>
                ingredient.RecipeId == recipeId && !ingredient.IsDeleted)).Should().Be(1);
            (await retryContext.RecipeSteps.CountAsync(step =>
                step.RecipeId == recipeId && !step.IsDeleted)).Should().Be(1);
        }
    }

    private static UpdateRecipeCommand CreateUpdateCommand(
        Guid recipeId,
        Guid categoryId,
        byte[] rowVersion,
        string title) =>
        new(
            recipeId,
            title,
            "Updated description",
            categoryId,
            10,
            20,
            2,
            "Easy",
            rowVersion,
            [new RecipeIngredientInput("Onion", 1, "piece", null, 0)],
            [new RecipeStepInput("Cook", "Cook the onion.", 5, null)]);

    private sealed class TestCurrentUser(string userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public bool IsAdmin => false;
        public bool IsAuthenticated => true;
    }

    private sealed class NoOpRecipeCache : IRecipeCache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(T));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
