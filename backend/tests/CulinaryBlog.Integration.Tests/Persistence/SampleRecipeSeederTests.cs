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
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            await using var serviceProvider = services.BuildServiceProvider();
            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await context.Database.MigrateAsync();

            var storage = new Mock<IFileStorageService>();
            storage.Setup(service => service.UploadAsync(
                    It.IsAny<FileUploadRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FileUploadRequest request, CancellationToken _) =>
                {
                    var objectName = $"sample/{Guid.NewGuid():N}.webp";
                    return new FileUploadResult(
                        objectName,
                        $"http://localhost:9000/culinary-blog/recipes/{objectName}",
                        "image/webp",
                        1);
                });

            await Lab02DataSeeder.SeedAsync(
                context,
                userManager,
                roleManager,
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);

            (await roleManager.RoleExistsAsync("Author")).Should().BeTrue();

            var recipe = await context.Recipes
                .Include(item => item.Images)
                .Include(item => item.Steps)
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Images.Should().ContainSingle(image => image.IsPrimary);
            recipe.Images.Where(image => image.StepNumber == 1).Should().HaveCount(2);
            recipe.Images.Where(image => image.StepNumber == 2).Should().ContainSingle();
            recipe.Steps.Single(step => step.StepNumber == 1).ImageUrl.Should()
                .Be(recipe.Images.Single(image => image.StepNumber == 1 && image.OrderIndex == 2).OriginalUrl);
            recipe.Steps.Single(step => step.StepNumber == 2).ImageUrl.Should()
                .Be(recipe.Images.Single(image => image.StepNumber == 2).OriginalUrl);
            recipe.Images.Single(image => image.OrderIndex == 1).StepNumber.Should().BeNull();

            foreach (var image in recipe.Images.Where(image => image.StepNumber.HasValue))
            {
                image.StepNumber = null;
            }
            await context.SaveChangesAsync();

            recipe.Steps.Single(step => step.StepNumber == 1).IsDeleted = true;
            context.RecipeSteps.Add(new CulinaryBlog.Domain.Entities.RecipeStep
            {
                RecipeId = recipe.Id,
                StepNumber = 1,
                Description = "Updated step.",
                ImageUrl = null
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await Lab02DataSeeder.SeedAsync(
                context,
                userManager,
                roleManager,
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);
            recipe = await context.Recipes
                .Include(item => item.Images)
                .Include(item => item.Steps)
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Images.Where(image => image.OrderIndex is 2 or 3 or 4)
                .Select(image => image.StepNumber)
                .Should().BeEquivalentTo(new int?[] { 1, 1, 2 });
            recipe.Steps.Single(step => step.StepNumber == 1).ImageUrl.Should()
                .Be(recipe.Images.Single(image => image.StepNumber == 1 && image.OrderIndex == 2).OriginalUrl);

            recipe.Nutrition.Should().NotBeNull();
            recipe.Nutrition!.Calories.Should().Be(310);

            recipe.Nutrition.Calories = 999;
            await context.SaveChangesAsync();
            await Lab02DataSeeder.SeedAsync(
                context,
                userManager,
                roleManager,
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
                userManager,
                roleManager,
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
                userManager,
                roleManager,
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
    public async Task SeedAsync_persists_recipe_without_nutrition_and_uses_sample_category_name()
    {
        container.Should().NotBeNull();
        var sampleDataPath = CreateSampleDataDirectory(includeNutrition: false, categorySlug: "mon-an-vat");
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(container!.GetConnectionString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            await using var serviceProvider = services.BuildServiceProvider();
            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
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
                userManager,
                roleManager,
                storage.Object,
                sampleDataPath,
                NullLogger.Instance);

            var recipe = await context.Recipes
                .Include(item => item.Nutrition)
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Nutrition.Should().BeNull();

            var category = await context.Categories.SingleAsync(item => item.Slug == "mon-an-vat");
            category.Name.Should().Be("Món ăn vặt");
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
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            await using var serviceProvider = services.BuildServiceProvider();
            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
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
                userManager,
                roleManager,
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

    private static string CreateSampleDataDirectory(
        bool includeNutrition = true,
        string categorySlug = "mon-test")
    {
        var sampleDataPath = Path.Combine(Path.GetTempPath(), $"sample-seed-{Guid.NewGuid():N}");
        var imageDirectory = Path.Combine(sampleDataPath, "images", "sample-recipe");
        Directory.CreateDirectory(imageDirectory);
        Directory.CreateDirectory(Path.Combine(sampleDataPath, "images", "danhmuc"));
        File.WriteAllBytes(Path.Combine(imageDirectory, "main.webp"), [1]);
        File.WriteAllBytes(Path.Combine(imageDirectory, "step1-preview.webp"), [1]);
        File.WriteAllBytes(Path.Combine(imageDirectory, "step1-1.webp"), [1]);
        File.WriteAllBytes(Path.Combine(imageDirectory, "step1-2.webp"), [1]);
        File.WriteAllBytes(Path.Combine(imageDirectory, "step2.webp"), [1]);
        File.WriteAllText(
            Path.Combine(sampleDataPath, "20_cong_thuc_mon_an.json"),
            $$"""
            [
              {
                "title": "Sample Recipe",
                "description": "A test recipe.",
                "categorySlug": "{{categorySlug}}",
                "author": "Test Author",
                "prepTimeMinutes": 1,
                "cookTimeMinutes": 2,
                "servings": 1,
                "difficulty": "Easy",
                {{(includeNutrition ? "\"nutrition\": { \"calories\": 310, \"protein\": 18.5, \"carbohydrates\": 36, \"fat\": 10.2, \"fiber\": 4.1, \"sodium\": 680 }," : "\"nutrition\": null,")}}
                "ingredients": [{ "name": "Ingredient", "quantity": 1, "unit": "piece", "orderIndex": 0 }],
                "steps": [
                  { "stepNumber": 1, "title": "Cook", "description": "Cook it.", "imageAsset": "step1-1.webp" },
                  { "stepNumber": 2, "title": "Serve", "description": "Serve it." }
                ],
                "folder": "sample_recipe",
                "images": [
                  { "file": "main.webp", "altText": "Finished recipe", "isPrimary": true, "orderIndex": 0 },
                  { "file": "step1-preview.webp", "altText": "Not a numbered step image", "isPrimary": false, "orderIndex": 1 },
                  { "file": "step1-1.webp", "altText": "Cooking step 1", "isPrimary": false, "orderIndex": 2 },
                  { "file": "step1-2.webp", "altText": "Cooking step 2", "isPrimary": false, "orderIndex": 3 },
                  { "file": "step2.webp", "altText": "Cooking step 2", "isPrimary": false, "orderIndex": 4 }
                ]
              }
            ]
            """,
            Encoding.UTF8);
        return sampleDataPath;
    }
}
