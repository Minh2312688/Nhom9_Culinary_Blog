using System.Text;
using CulinaryBlog.Application.Contracts.Storage;
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
                .SingleAsync(item => item.Slug == "sample-recipe");
            recipe.Images.Should().ContainSingle(image => image.IsPrimary);
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
