using System.Text;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;
using Category = CulinaryBlog.Domain.Entities.Category;

namespace CulinaryBlog.Integration.Tests.Persistence;

public sealed class SampleRecipeSeederTests : IAsyncLifetime
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
    public async Task SeedAsync_persists_sample_image_for_new_recipe()
    {
        container.Should().NotBeNull();
        var sampleDataPath = CreateSampleDataDirectory();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(container!.GetConnectionString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            await using var serviceProvider = services.BuildServiceProvider();
            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();

            var storage = new Mock<IFileStorageService>();
            storage.Setup(service => service.UploadAsync(
                    It.IsAny<FileUploadRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileUploadResult(
                    "sample/main.webp",
                    "http://localhost:9000/culinary-blog/recipes/sample/main.webp",
                    "image/webp",
                    1));

            await Lab02DataSeeder.SeedAsync(
                context,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);

            var recipe = await context.Recipes
                .Include(item => item.Images)
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Images.Should().ContainSingle(image => image.IsPrimary);
            recipe.Nutrition.Should().NotBeNull();
            recipe.Nutrition!.Calories.Should().Be(310);

            recipe.Nutrition.Calories = 999;
            await context.SaveChangesAsync();
            await Lab02DataSeeder.SeedAsync(
                context,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);
            recipe = await context.Recipes
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Nutrition!.Calories.Should().Be(999);

            context.RecipeNutritions.Remove(recipe.Nutrition);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var nutritionStillExists = await context.RecipeNutritions
                .AnyAsync(item => item.RecipeId == recipe.Id);
            nutritionStillExists.Should().BeFalse();
            await Lab02DataSeeder.SeedAsync(
                context,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);
            recipe = await context.Recipes
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Nutrition.Should().NotBeNull();
            recipe.Nutrition!.Calories.Should().Be(310);

            await context.RecipeNutritions
                .Where(item => item.RecipeId == recipe.Id)
                .ExecuteDeleteAsync();
            context.ChangeTracker.Clear();
            var nutritionExistsAfterHardDelete = await context.RecipeNutritions
                .IgnoreQueryFilters()
                .AnyAsync(item => item.RecipeId == recipe.Id);
            nutritionExistsAfterHardDelete.Should().BeFalse();
            await Lab02DataSeeder.SeedAsync(
                context,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);
            context.ChangeTracker.Clear();
            recipe = await context.Recipes
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Nutrition.Should().NotBeNull();
            recipe.Nutrition!.Calories.Should().Be(310);
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    [DockerPostgresFact]
    public async Task SeedAsync_uploads_matching_category_image_without_overwriting_existing_image()
    {
        container.Should().NotBeNull();
        var sampleDataPath = CreateSampleDataDirectory();
        try
        {
            var categoryImagesPath = Path.Combine(sampleDataPath, "images", "danhmuc");
            File.WriteAllBytes(Path.Combine(categoryImagesPath, "mon-test.jpg"), [1]);
            File.WriteAllBytes(Path.Combine(categoryImagesPath, "mon-viet.webp"), [1]);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(container!.GetConnectionString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            await using var serviceProvider = services.BuildServiceProvider();
            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();
            var existingImageCategory = Category.Create("Món Việt");
            existingImageCategory.ImageUrl = "https://media.example.test/categories/custom.webp";
            context.Categories.Add(existingImageCategory);
            await context.SaveChangesAsync();

            var storage = new Mock<IFileStorageService>();
            storage.Setup(service => service.UploadAsync(
                    It.IsAny<FileUploadRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FileUploadRequest request, CancellationToken _) =>
                    new FileUploadResult(
                        request.FileName,
                        $"https://media.example.test/{request.Folder}/{request.FileName}",
                        request.ContentType ?? "image/jpeg",
                        request.Length));

            await Lab02DataSeeder.SeedAsync(
                context,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);

            var categoryWithSeedImage = await context.Categories.SingleAsync(item => item.Slug == "mon-test");
            var categoryWithExistingImage = await context.Categories.SingleAsync(item => item.Slug == "mon-viet");
            categoryWithSeedImage.ImageUrl.Should()
                .Be("https://media.example.test/categories/mon-test.jpg");
            categoryWithExistingImage.ImageUrl.Should()
                .Be("https://media.example.test/categories/custom.webp");
            storage.Verify(service => service.UploadAsync(
                It.Is<FileUploadRequest>(request => request.Folder == StorageFolders.Categories),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(sampleDataPath, recursive: true);
        }
    }

    private static string CreateSampleDataDirectory()
    {
        var sampleDataPath = Path.Combine(Path.GetTempPath(), $"sample-seed-{Guid.NewGuid():N}");
        var imageDirectory = Path.Combine(sampleDataPath, "images", "sample-recipe");
        Directory.CreateDirectory(imageDirectory);
        Directory.CreateDirectory(Path.Combine(sampleDataPath, "images", "danhmuc"));
        File.WriteAllBytes(Path.Combine(imageDirectory, "main.webp"), [1]);
        File.WriteAllText(
            Path.Combine(sampleDataPath, "20_cong_thuc_mon_an.json"),
            """
            [
              {
                "title": "Sample Recipe",
                "description": "A test recipe.",
                "categorySlug": "mon-test",
                "author": "Test Author",
                "prepTimeMinutes": 1,
                "cookTimeMinutes": 2,
                "servings": 1,
                "difficulty": "Easy",
                "nutrition": { "calories": 310, "protein": 18.5, "carbohydrates": 36, "fat": 10.2, "fiber": 4.1, "sodium": 680 },
                "ingredients": [{ "name": "Ingredient", "quantity": 1, "unit": "piece", "orderIndex": 0 }],
                "steps": [{ "stepNumber": 1, "description": "Cook it." }],
                "folder": "sample_recipe",
                "images": [{ "file": "main.webp", "altText": "Finished recipe", "isPrimary": true, "orderIndex": 0 }]
              }
            ]
            """,
            Encoding.UTF8);
        return sampleDataPath;
    }
}
