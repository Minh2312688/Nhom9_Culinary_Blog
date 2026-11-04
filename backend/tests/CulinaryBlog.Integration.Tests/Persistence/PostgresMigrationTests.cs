using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using ApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;

namespace CulinaryBlog.Integration.Tests.Persistence;

public sealed class PostgresMigrationTests : IAsyncLifetime
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
    public async Task Database_MigratesSuccessfully_AndEnforcesSchemaOnPostgres16()
    {
        container.Should().NotBeNull();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container!.GetConnectionString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();

        // 1. Kiểm tra kết nối và bảng
        var canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue();

        // 2. Thêm người dùng, danh mục và công thức để xác nhận mapping hoạt động trơn tru
        var author = new ApplicationUser
        {
            Id = "test-author",
            UserName = "testauthor",
            Email = "test@example.com",
            DisplayName = "Test Author"
        };
        db.Users.Add(author);
        var category = Category.Create("Test Category", "A test category description");
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var recipe = new Recipe
        {
            Title = "Postgres Migration Soup",
            Slug = "postgres-migration-soup",
            CategoryId = category.Id,
            AuthorId = author.Id,
            Difficulty = RecipeDifficulty.Easy,
            Servings = 4
        };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        recipe.Id.Should().NotBeEmpty();
        recipe.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // 3. Kiểm tra cột RowVersion và SearchVector tồn tại trên PostgreSQL thật
        await using var conn = new NpgsqlConnection(container.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'Recipes' AND column_name IN ('RowVersion', 'SearchVector')", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var columns = new Dictionary<string, string>();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        columns.Should().ContainKey("RowVersion");
        columns.Should().ContainKey("SearchVector");
        columns["SearchVector"].Should().Be("tsvector");
    }
}
