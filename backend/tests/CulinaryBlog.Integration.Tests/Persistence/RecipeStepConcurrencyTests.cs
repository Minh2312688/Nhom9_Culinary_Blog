using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

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
            var category = Category.Create("Desserts", null);
            var recipe = new Recipe
            {
                Title = "Chocolate Cake",
                Slug = "chocolate-cake",
                CategoryId = category.Id,
                AuthorId = "baker-1",
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

        // 4. Context B cập nhật dữ liệu dựa trên row version cũ -> phải ném DbUpdateConcurrencyException
        stepB.Description = "Preheat oven to 325F (conflicting update by B)";
        var action = async () => await contextB.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
