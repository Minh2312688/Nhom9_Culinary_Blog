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

    [DockerPostgresFact]
    public async Task Database_MigratesExistingData_WithoutLossOrFabricatingValues()
    {
        container.Should().NotBeNull();
        var connectionString = container!.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync("20260921083055_InitialRecipeSchema");

        var authorId = "legacy-author";
        var categoryId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var createdAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var originalRowVersion = new byte[] { 0x2A };

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            await using (var command = new NpgsqlCommand("""
                INSERT INTO "ApplicationUser" (
                    "Id", "DisplayName", "IsActive", "CreatedAt", "Role",
                    "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                    "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled",
                    "LockoutEnabled", "AccessFailedCount")
                VALUES (
                    @id, 'Legacy Chef', TRUE, @createdAt, '  Admin  ',
                    'legacychef', 'LEGACYCHEF', 'legacy@example.com', 'LEGACY@EXAMPLE.COM',
                    TRUE, FALSE, FALSE, TRUE, 0)
                """, conn))
            {
                command.Parameters.AddWithValue("id", authorId);
                command.Parameters.AddWithValue("createdAt", createdAt);
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new NpgsqlCommand("""
                INSERT INTO "Categories" ("Id", "Name", "Slug", "Description", "CreatedAt")
                VALUES (@id, 'Legacy Category', 'legacy-category', 'Existing category', @createdAt)
                """, conn))
            {
                command.Parameters.AddWithValue("id", categoryId);
                command.Parameters.AddWithValue("createdAt", createdAt);
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new NpgsqlCommand("""
                INSERT INTO "Recipes" (
                    "Id", "Title", "Slug", "Description", "PrepTimeMinutes",
                    "CookTimeMinutes", "Servings", "Difficulty", "Status",
                    "CategoryId", "AuthorId", "CreatedAt", "IsDeleted", "RowVersion")
                VALUES (
                    @id, 'Phở gà truyền thống', 'legacy-pho-ga', 'Món ăn gia đình',
                    15, 30, 4, 'Easy', 1, @categoryId, @authorId, @createdAt,
                    FALSE, @rowVersion)
                """, conn))
            {
                command.Parameters.AddWithValue("id", recipeId);
                command.Parameters.AddWithValue("categoryId", categoryId);
                command.Parameters.AddWithValue("authorId", authorId);
                command.Parameters.AddWithValue("createdAt", createdAt);
                command.Parameters.AddWithValue("rowVersion", originalRowVersion);
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = new NpgsqlCommand("""
                INSERT INTO "RecipeImages" (
                    "Id", "RecipeId", "OriginalUrl", "IsPrimary", "OrderIndex",
                    "CreatedAt", "IsDeleted", "RowVersion")
                VALUES (@id, @recipeId, 'https://example.test/legacy.jpg', TRUE, 0,
                    @createdAt, FALSE, @rowVersion)
                """, conn))
            {
                command.Parameters.AddWithValue("id", imageId);
                command.Parameters.AddWithValue("recipeId", recipeId);
                command.Parameters.AddWithValue("createdAt", createdAt);
                command.Parameters.AddWithValue("rowVersion", originalRowVersion);
                await command.ExecuteNonQueryAsync();
            }
        }

        await db.Database.MigrateAsync("20260930120000_AddRecipeFullTextSearch");

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT "SearchVector" @@ to_tsquery('simple', 'pho'),
                       "RowVersion"
                FROM "Recipes"
                WHERE "Id" = @id
                """, conn);
            command.Parameters.AddWithValue("id", recipeId);

            await using var reader = await command.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetBoolean(0).Should().BeTrue();
            reader.GetFieldValue<byte[]>(1).Should().Equal(originalRowVersion);
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            await using (var command = new NpgsqlCommand("""
                SELECT u."DisplayName", u."Email", r."NormalizedName"
                FROM "AspNetUsers" u
                JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                WHERE u."Id" = @authorId
                """, conn))
            {
                command.Parameters.AddWithValue("authorId", authorId);
                await using var reader = await command.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();
                reader.GetString(0).Should().Be("Legacy Chef");
                reader.GetString(1).Should().Be("legacy@example.com");
                reader.GetString(2).Should().Be("ADMIN");
            }

            await using (var command = new NpgsqlCommand("""
                SELECT "AuthorId", "PublishedAt", "RowVersion"
                FROM "Recipes"
                WHERE "Id" = @id
                """, conn))
            {
                command.Parameters.AddWithValue("id", recipeId);
                await using var reader = await command.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();
                reader.GetString(0).Should().Be(authorId);
                reader.IsDBNull(1).Should().BeTrue();
                reader.GetFieldValue<byte[]>(2).Should().Equal(originalRowVersion);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT "VariantStatus"
                FROM "RecipeImages"
                WHERE "Id" = @id
                """, conn))
            {
                command.Parameters.AddWithValue("id", imageId);
                (await command.ExecuteScalarAsync()).Should().Be("Pending");
            }

            await using (var command = new NpgsqlCommand("""
                UPDATE "Recipes"
                SET "Title" = 'Bún bò Huế'
                WHERE "Id" = @id
                """, conn))
            {
                command.Parameters.AddWithValue("id", recipeId);
                (await command.ExecuteNonQueryAsync()).Should().Be(1);
            }

            await using (var command = new NpgsqlCommand("""
                SELECT "SearchVector" @@ to_tsquery('simple', 'bun'),
                       "SearchVector" @@ to_tsquery('simple', 'pho')
                FROM "Recipes"
                WHERE "Id" = @id
                """, conn))
            {
                command.Parameters.AddWithValue("id", recipeId);
                await using var reader = await command.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();
                reader.GetBoolean(0).Should().BeTrue();
                reader.GetBoolean(1).Should().BeFalse();
            }
        }
    }
}
