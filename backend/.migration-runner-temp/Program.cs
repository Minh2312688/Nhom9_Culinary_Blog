using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings__Postgres is not configured.");
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connectionString, provider => provider.MigrationsAssembly("CulinaryBlog.Infrastructure"))
    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
    .Options;

await using var db = new ApplicationDbContext(options);
Console.WriteLine("Applied migrations before update:");
foreach (var migration in await db.Database.GetAppliedMigrationsAsync()) Console.WriteLine(migration);

await db.Database.MigrateAsync();

var results = await new RecipeRepository(db).SearchAsync("banh", null, false, 1, 12);
Console.WriteLine($"PostgreSQL search query executed; matching rows: {results.TotalCount}.");

var accentMatches = await db.Database.SqlQueryRaw<bool>("""
    SELECT to_tsvector('simple', unaccent('Bánh chưng'))
        @@ to_tsquery('simple', unaccent('banh:*')) AS "Value"
    """).SingleAsync();
var reverseAccentMatches = await db.Database.SqlQueryRaw<bool>("""
    SELECT to_tsvector('simple', unaccent('banh chung'))
        @@ to_tsquery('simple', unaccent('bánh:*')) AS "Value"
    """).SingleAsync();
Console.WriteLine($"Accent normalization check: query without accents={accentMatches}, with accents={reverseAccentMatches}.");

await using (var transaction = await db.Database.BeginTransactionAsync())
{
    var userId = $"fts-smoke-{Guid.NewGuid():N}";
    var categoryId = Guid.NewGuid();
    var recipeId = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "ApplicationUser" ("Id", "DisplayName", "IsActive", "CreatedAt", "Role",
            "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
        VALUES ({userId}, 'FTS smoke user', true, now(), 'Author', false, false, false, false, 0)
        """);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "Categories" ("Id", "Name", "Slug", "CreatedAt")
        VALUES ({categoryId}, 'FTS smoke', {userId}, now())
        """);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "Recipes" ("Id", "Title", "Slug", "Description", "PrepTimeMinutes", "CookTimeMinutes",
            "Servings", "Difficulty", "Status", "CategoryId", "AuthorId", "CreatedAt", "IsDeleted", "RowVersion")
        VALUES ({recipeId}, 'Bánh Chưng Search Smoke', {userId}, 'Unique FTS test dish', 1, 1,
            2, 'Easy', 1, {categoryId}, {userId}, now(), false, decode('00', 'hex'))
        """);

    var smokeRepository = new RecipeRepository(db);
    var accentSearch = await smokeRepository.SearchAsync("banh", null, false, 1, 12);
    var exactSearch = await smokeRepository.SearchAsync("chung", null, false, 1, 12);
    if (accentSearch.TotalCount != 1 || exactSearch.TotalCount != 1)
        throw new InvalidOperationException($"FTS insert smoke failed: accent={accentSearch.TotalCount}, token={exactSearch.TotalCount}.");

    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Recipes\" SET \"Title\" = 'Pho Search Smoke' WHERE \"Id\" = {recipeId}");
    var updatedOldTerm = await smokeRepository.SearchAsync("banh", null, false, 1, 12);
    var updatedNewTerm = await smokeRepository.SearchAsync("pho", null, false, 1, 12);
    if (updatedOldTerm.TotalCount != 0 || updatedNewTerm.TotalCount != 1)
        throw new InvalidOperationException($"FTS update trigger smoke failed: old={updatedOldTerm.TotalCount}, new={updatedNewTerm.TotalCount}.");
    Console.WriteLine("FTS insert/search/update smoke passed; transaction will be rolled back.");
    await transaction.RollbackAsync();
}

Console.WriteLine("Applied migrations after update:");
foreach (var migration in await db.Database.GetAppliedMigrationsAsync()) Console.WriteLine(migration);
