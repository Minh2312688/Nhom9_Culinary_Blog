using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set POSTGRES_CONNECTION_STRING to the intended verification database.");
    return 2;
}

if (Environment.GetEnvironmentVariable("MIGRATION_ROLLBACK_VERIFY") == "1")
    return await VerifyMigrationRollbackAsync(connectionString);

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
{
    await readOnly.ExecuteNonQueryAsync();
}

var failed = false;
var checks = new List<(string Name, bool Passed, string? Detail)>();
void Check(string name, bool passed, string? detail = null)
{
    checks.Add((name, passed, detail));
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}{(detail is null ? string.Empty : $": {detail}")}");
    failed |= !passed;
}

var extension = await ScalarAsync<bool>(connection, transaction,
    "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'unaccent')");
var vectorColumn = await ScalarAsync<bool>(connection, transaction, """
    SELECT EXISTS (
        SELECT 1 FROM pg_attribute
        WHERE attrelid = '"Recipes"'::regclass AND attname = 'SearchVector'
            AND atttypid = 'tsvector'::regtype AND NOT attisdropped)
    """);
var vectorNulls = await ScalarAsync<long>(connection, transaction,
    "SELECT COUNT(*) FROM \"Recipes\" WHERE \"SearchVector\" IS NULL");
var ginIndex = await ScalarAsync<bool>(connection, transaction, """
    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = current_schema() AND tablename = 'Recipes'
            AND indexdef ILIKE '%USING gin%' AND indexdef ILIKE '%SearchVector%')
    """);
var vectorTrigger = await ScalarAsync<bool>(connection, transaction, """
    SELECT EXISTS (
        SELECT 1 FROM pg_trigger
        WHERE tgname = 'trg_recipes_search_vector' AND NOT tgisinternal)
    """);
var migrationApplied = await ScalarAsync<bool>(connection, transaction, """
    SELECT EXISTS (
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20260930120000_AddRecipeFullTextSearch')
    """);
var accentNormalization = await ScalarAsync<bool>(connection, transaction,
    "SELECT unaccent('pho') = unaccent('phở')");
var accentInsensitiveFts = await ScalarAsync<bool>(connection, transaction, """
    SELECT
        to_tsvector('simple', unaccent('Bánh mì bò')) @@
            to_tsquery('simple', unaccent('banh:* & mi:*'))
        AND to_tsvector('simple', unaccent('banh mi bo')) @@
            to_tsquery('simple', unaccent('bánh:* & mì:*'))
    """);
var databaseName = await ScalarAsync<string>(connection, transaction, "SELECT current_database()");
var difficultyType = await ScalarAsync<string>(connection, transaction, """
    SELECT data_type FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'Recipes' AND column_name = 'Difficulty'
    """);
var statusType = await ScalarAsync<string>(connection, transaction, """
    SELECT data_type FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'Recipes' AND column_name = 'Status'
    """);
var recipeColumnTypes = new List<string>();
await using (var columnCommand = new NpgsqlCommand("""
    SELECT column_name || '=' || data_type
    FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'Recipes'
        AND column_name IN ('Id', 'Title', 'Slug', 'Description', 'Difficulty', 'CookTimeMinutes',
            'Servings', 'Status', 'CategoryId', 'AuthorId', 'RowVersion')
    ORDER BY ordinal_position
    """, connection, transaction))
await using (var columnReader = await columnCommand.ExecuteReaderAsync())
{
    while (await columnReader.ReadAsync()) recipeColumnTypes.Add(columnReader.GetString(0));
}

Check("unaccent extension", extension);
Check("SearchVector tsvector column", vectorColumn);
Check("all search vectors populated", vectorNulls == 0, $"null rows={vectorNulls}");
Check("GIN index", ginIndex);
Check("search vector trigger", vectorTrigger);
Check("FTS migration recorded", migrationApplied);
Check("Vietnamese accent normalization", accentNormalization);
Check("accent-insensitive FTS in both directions", accentInsensitiveFts);
var leftoverVerificationRecipes = await ScalarAsync<long>(connection, transaction,
    "SELECT COUNT(*) FROM \"Recipes\" WHERE \"Slug\" LIKE 'search-verify-%'");
Check("no temporary verification recipes remain", leftoverVerificationRecipes == 0,
    $"remaining={leftoverVerificationRecipes}");

var samples = new List<(string Title, string? Description)>();
await using (var sampleCommand = new NpgsqlCommand("""
    SELECT "Title", "Description"
    FROM "Recipes"
    WHERE "Status" = 1 AND "SearchVector" IS NOT NULL
    ORDER BY "CreatedAt" DESC
    LIMIT 5000
    """, connection, transaction))
await using (var reader = await sampleCommand.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
    {
        samples.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
    }
}

Check("published recipe corpus", samples.Count > 0, $"rows={samples.Count}");
if (samples.Count > 0)
{
    var tokens = Regex.Matches(samples[0].Title, @"[\p{L}\p{N}]+")
        .Select(match => match.Value)
        .ToArray();
    Check("sample title contains a searchable token", tokens.Length > 0, samples[0].Title);

    if (tokens.Length > 0)
    {
        var fullTokenQuery = $"{tokens[0]}:*";
        var fullTokenMatches = await CountMatchesAsync(connection, transaction, fullTokenQuery);
        Check("prefix search matches sample title", fullTokenMatches > 0, $"matches={fullTokenMatches}");

        var prefixLength = Math.Min(3, tokens[0].Length);
        var shortPrefixQuery = $"{tokens[0][..prefixLength]}:*";
        var prefixMatches = await CountMatchesAsync(connection, transaction, shortPrefixQuery);
        Check("shorter prefix search", prefixMatches >= fullTokenMatches,
            $"short-prefix={prefixMatches}, full-token={fullTokenMatches}");

        if (tokens.Length > 1)
        {
            var andQuery = $"{tokens[0]}:* & {tokens[1]}:*";
            var andMatches = await CountMatchesAsync(connection, transaction, andQuery);
            Check("AND search matches sample title", andMatches > 0, $"matches={andMatches}");
        }

        var accentedTitle = samples.FirstOrDefault(sample =>
            Regex.Matches(sample.Title, @"[\p{L}\p{N}]+")
                .Any(match => match.Value.Any(character => character > 127)));
        var accentedToken = accentedTitle == default
            ? null
            : Regex.Matches(accentedTitle.Title, @"[\p{L}\p{N}]+")
                .Select(match => match.Value)
                .FirstOrDefault(token => token.Any(character => character > 127));
        if (accentedToken is not null)
        {
            var unaccentedToken = RemoveDiacritics(accentedToken);
            var accentedMatches = await CountMatchesAsync(connection, transaction, $"{accentedToken}:*");
            var unaccentedMatches = await CountMatchesAsync(connection, transaction, $"{unaccentedToken}:*");
            Check("accented and unaccented search are equivalent",
                accentedMatches > 0 && accentedMatches == unaccentedMatches,
                $"accented={accentedMatches}, unaccented={unaccentedMatches}");
        }
        else
        {
            Console.WriteLine("SKIP accented search equivalence: sample titles contain no accented token.");
        }

        await using (var plannerSetting = new NpgsqlCommand("SET LOCAL enable_seqscan = off", connection, transaction))
        {
            await plannerSetting.ExecuteNonQueryAsync();
        }

        var planLines = new List<string>();
        await using (var explain = new NpgsqlCommand("""
            EXPLAIN (COSTS OFF)
            SELECT "Id" FROM "Recipes"
            WHERE "SearchVector" @@ to_tsquery('simple', unaccent(@query))
            """, connection, transaction))
        {
            explain.Parameters.AddWithValue("query", fullTokenQuery);
            await using var planReader = await explain.ExecuteReaderAsync();
            while (await planReader.ReadAsync()) planLines.Add(planReader.GetString(0));
        }
        var usesGin = planLines.Any(line => line.Contains("IDX_Recipe_Search", StringComparison.OrdinalIgnoreCase));
        Check("query plan uses the FTS GIN index", usesGin, string.Join(" | ", planLines));
    }
}

var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connection)
    .Options;
await using (var db = new ApplicationDbContext(dbOptions))
{
    await db.Database.UseTransactionAsync(transaction);
    var listQuery = db.Recipes.AsNoTracking()
        .Where(recipe => recipe.Status == RecipeStatus.Published)
        .OrderByDescending(recipe => recipe.CreatedAt)
        .Take(3)
        .Select(recipe => new
        {
            recipe.Id, recipe.Title, recipe.Slug, recipe.Description, recipe.Difficulty,
            recipe.CookTimeMinutes, recipe.Servings, recipe.Status, recipe.CategoryId,
            recipe.AuthorId, recipe.RowVersion
        });
    Console.WriteLine("List projection SQL: " + listQuery.ToQueryString().Replace(Environment.NewLine, " | "));
    var listRows = await listQuery.ToListAsync();
    Check("EF repository list projection materializes", listRows.Count > 0, $"rows={listRows.Count}");

    var repository = new RecipeRepository(db);
    var queryHandler = new GetRecipesQueryHandler(repository, new VerificationCurrentUser());
    var handlerList = await queryHandler.Handle(new GetRecipesQuery(PageSize: 3), CancellationToken.None);
    Check("GetRecipesQueryHandler list path executes", handlerList.Items.Count > 0,
        $"total={handlerList.TotalCount}, page-items={handlerList.Items.Count}");

    var searchResult = await repository.SearchAsync(
        "Report", null, false, 1, 12, null, null, null, null, CancellationToken.None);
    Check("EF repository search executes", searchResult.TotalCount > 0 && searchResult.Items.Count == 12,
        $"total={searchResult.TotalCount}, page-items={searchResult.Items.Count}");

    var firstMatch = searchResult.Items[0];
    var difficulty = Enum.Parse<RecipeDifficulty>(firstMatch.Difficulty, true);
    var filteredResult = await repository.SearchAsync(
        "Report", null, false, 1, 12, firstMatch.CategoryId, difficulty,
        firstMatch.CookTimeMinutes, firstMatch.Servings, CancellationToken.None);
    Check("EF repository search applies combined filters",
        filteredResult.Items.Any(item => item.Id == firstMatch.Id),
        $"total={filteredResult.TotalCount}");

    var noMatch = await repository.SearchAsync(
        "zzverificationnomatch", null, false, 1, 12, null, null, null, null, CancellationToken.None);
    Check("EF repository returns a successful empty page", noMatch.TotalCount == 0 && noMatch.Items.Count == 0,
        $"total={noMatch.TotalCount}, page-items={noMatch.Items.Count}");

    var privateSamples = await db.Recipes.AsNoTracking()
        .Where(recipe => recipe.Status == RecipeStatus.Draft || recipe.Status == RecipeStatus.Archived)
        .OrderBy(recipe => recipe.Status)
        .Select(recipe => new { recipe.Id, recipe.AuthorId, recipe.Status, recipe.Title })
        .ToListAsync();
    async Task<bool> ListContainsAsync(Guid id, string? userId, bool isAdmin)
    {
        var handler = new GetRecipesQueryHandler(repository, new VerificationCurrentUser(userId, isAdmin));
        var firstPage = await handler.Handle(new GetRecipesQuery(PageSize: 50), CancellationToken.None);
        if (firstPage.Items.Any(item => item.Id == id)) return true;
        var pageCount = (int)Math.Ceiling(firstPage.TotalCount / 50d);
        for (var page = 2; page <= pageCount; page++)
        {
            var result = await handler.Handle(new GetRecipesQuery(Page: page, PageSize: 50), CancellationToken.None);
            if (result.Items.Any(item => item.Id == id)) return true;
        }
        return false;
    }
    var visibilityGroups = privateSamples.GroupBy(recipe => recipe.Status).ToArray();
    foreach (var group in visibilityGroups)
    {
        var sample = group.First();
        var tokens = Regex.Matches(sample.Title, @"[\p{L}\p{N}]+")
            .Select(match => match.Value).ToArray();
        if (tokens.Length == 0) continue;
        var token = string.Join(' ', tokens);

        var guestSearch = await repository.SearchAsync(token, null, false, 1, 50,
            null, null, null, null, CancellationToken.None);
        var authorSearch = await repository.SearchAsync(token, sample.AuthorId, false, 1, 50,
            null, null, null, null, CancellationToken.None);
        var adminSearch = await repository.SearchAsync(token, "verification-admin", true, 1, 50,
            null, null, null, null, CancellationToken.None);
        var statusLabel = group.Key.ToString();
        Check($"PostgreSQL search hides {statusLabel} from guest",
            guestSearch.Items.All(item => item.Id != sample.Id));
        Check($"PostgreSQL search shows {statusLabel} to author",
            authorSearch.Items.Any(item => item.Id == sample.Id));
        Check($"PostgreSQL search shows {statusLabel} to admin",
            adminSearch.Items.Any(item => item.Id == sample.Id));

        Check($"PostgreSQL list hides {statusLabel} from guest",
            !await ListContainsAsync(sample.Id, null, false));
        Check($"PostgreSQL list shows {statusLabel} to author",
            await ListContainsAsync(sample.Id, sample.AuthorId, false));
        Check($"PostgreSQL list shows {statusLabel} to admin",
            await ListContainsAsync(sample.Id, "verification-admin", true));
    }
    Check("PostgreSQL existing private recipe corpus is optional",
        visibilityGroups.Length == 0 ||
        (visibilityGroups.Any(group => group.Key == RecipeStatus.Draft) &&
         visibilityGroups.Any(group => group.Key == RecipeStatus.Archived)),
        $"statuses={string.Join(',', visibilityGroups.Select(group => group.Key))}; temporary corpus runs below");
}

await transaction.RollbackAsync();
await VerifyTemporaryVisibilityAsync(connectionString, Check);
var apiBaseUrl = Environment.GetEnvironmentVariable("SEARCH_VERIFICATION_API_URL");
if (!string.IsNullOrWhiteSpace(apiBaseUrl))
{
    await VerifyTemporaryVietnameseRecipeApiAsync(connectionString, apiBaseUrl, Check);
}
else
{
    Console.WriteLine("SKIP live API recipe verification: set SEARCH_VERIFICATION_API_URL to opt in to temporary recipe creation and cleanup.");
}

Console.WriteLine("Schema/FTS verification: " + string.Join("; ", checks.Take(11)
    .Select(check => $"{check.Name}={(check.Passed ? "OK" : "FAILED")}")));
Console.WriteLine($"Database schema: database={databaseName}; Difficulty={difficultyType}; Status={statusType}");
Console.WriteLine("Recipe projection column types: " + string.Join("; ", recipeColumnTypes));
return failed ? 1 : 0;

static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    var result = await command.ExecuteScalarAsync();
    return (T)Convert.ChangeType(result!, typeof(T));
}

static async Task<int> VerifyMigrationRollbackAsync(string connectionString)
{
    const string migration = "20260930120000_AddRecipeFullTextSearch";
    const string previousMigration = "20260921101739_AddRowVersionDefaults";
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    async Task<(bool Column, bool Index, bool Trigger, bool Function, bool History)> ReadStateAsync()
    {
        await using var command = new NpgsqlCommand("""
            SELECT
                EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid = '"Recipes"'::regclass
                    AND attname = 'SearchVector' AND NOT attisdropped),
                EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'Recipes'
                    AND indexname = 'IDX_Recipe_Search'),
                EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_recipes_search_vector'
                    AND NOT tgisinternal),
                EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'update_recipe_search_vector'),
                EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = @migration)
            """, connection);
        command.Parameters.AddWithValue("migration", migration);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2),
            reader.GetBoolean(3), reader.GetBoolean(4));
    }

    var before = await ReadStateAsync();
    Console.WriteLine($"Before rollback: SearchVector={before.Column}; GIN={before.Index}; Trigger={before.Trigger}; Function={before.Function}; Migration={before.History}");

    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
        .Options;
    await using (var db = new ApplicationDbContext(options))
    {
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(previousMigration);
    }

    var after = await ReadStateAsync();
    await using var previousCommand = new NpgsqlCommand("""
        SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = @migration)
        """, connection);
    previousCommand.Parameters.AddWithValue("migration", previousMigration);
    var previousApplied = (bool)(await previousCommand.ExecuteScalarAsync())!;
    Console.WriteLine($"After rollback: SearchVector={after.Column}; GIN={after.Index}; Trigger={after.Trigger}; Function={after.Function}; Migration={after.History}; PreviousMigration={previousApplied}");

    var passed = before.Column && before.Index && before.Trigger && before.Function && before.History &&
        !after.Column && !after.Index && !after.Trigger && !after.Function && !after.History && previousApplied;
    Console.WriteLine(passed
        ? "PASS migration rollback removed FTS schema and retained previous migration"
        : "FAIL migration rollback state did not match expected result");
    return passed ? 0 : 1;
}

static async Task VerifyTemporaryVisibilityAsync(
    string connectionString,
    Action<string, bool, string?> check)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    try
    {
        await using var source = new NpgsqlCommand("""
            SELECT "CategoryId", "AuthorId"
            FROM "Recipes"
            WHERE NOT "IsDeleted" AND "Status" = 1
            LIMIT 1
            """, connection, transaction);
        await using var sourceReader = await source.ExecuteReaderAsync();
        if (!await sourceReader.ReadAsync())
        {
            check("temporary PostgreSQL visibility seed has published source", false, "no published recipe available");
            await transaction.RollbackAsync();
            return;
        }

        var categoryId = sourceReader.GetGuid(0);
        var authorId = sourceReader.GetString(1);
        await sourceReader.CloseAsync();

        var token = Guid.NewGuid().ToString("N")[..12];
        var draftId = Guid.NewGuid();
        var archivedId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO "Recipes"
                ("Id", "Title", "Slug", "Description", "PrepTimeMinutes", "CookTimeMinutes", "Servings",
                 "Difficulty", "Status", "CategoryId", "AuthorId", "ApplicationUserId", "CreatedAt",
                 "UpdatedAt", "IsDeleted")
            VALUES
                (@draftId, @draftTitle, @draftSlug, 'Temporary visibility verification', 10, 20, 2,
                 'Easy', 0, @categoryId, @authorId, NULL, now(), NULL, false),
                (@archivedId, @archivedTitle, @archivedSlug, 'Temporary visibility verification', 10, 20, 2,
                 'Easy', 2, @categoryId, @authorId, NULL, now(), NULL, false)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("draftId", draftId);
            insert.Parameters.AddWithValue("draftTitle", $"Visibility draft {token}");
            insert.Parameters.AddWithValue("draftSlug", $"visibility-draft-{token}");
            insert.Parameters.AddWithValue("archivedId", archivedId);
            insert.Parameters.AddWithValue("archivedTitle", $"Visibility archived {token}");
            insert.Parameters.AddWithValue("archivedSlug", $"visibility-archived-{token}");
            insert.Parameters.AddWithValue("categoryId", categoryId);
            insert.Parameters.AddWithValue("authorId", authorId);
            await insert.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.UseTransactionAsync(transaction);
        var repository = new RecipeRepository(db);

        async Task<bool> SearchContainsAsync(Guid id, string? userId, bool isAdmin)
        {
            var handler = new GetRecipesQueryHandler(repository, new VerificationCurrentUser(userId, isAdmin));
            var result = await handler.Handle(new GetRecipesQuery(Search: $"Visibility {token}", PageSize: 50), CancellationToken.None);
            return result.Items.Any(item => item.Id == id);
        }

        async Task<bool> ListContainsAsync(Guid id, string? userId, bool isAdmin)
        {
            var handler = new GetRecipesQueryHandler(repository, new VerificationCurrentUser(userId, isAdmin));
            var firstPage = await handler.Handle(new GetRecipesQuery(PageSize: 50), CancellationToken.None);
            if (firstPage.Items.Any(item => item.Id == id)) return true;
            var pageCount = (int)Math.Ceiling(firstPage.TotalCount / 50d);
            for (var page = 2; page <= pageCount; page++)
            {
                var pageResult = await handler.Handle(new GetRecipesQuery(Page: page, PageSize: 50), CancellationToken.None);
                if (pageResult.Items.Any(item => item.Id == id)) return true;
            }
            return false;
        }

        foreach (var sample in new[]
        {
            (Id: draftId, Status: "Draft"),
            (Id: archivedId, Status: "Archived")
        })
        {
            check($"PostgreSQL search hides temporary {sample.Status} from guest",
                !await SearchContainsAsync(sample.Id, null, false), null);
            check($"PostgreSQL search shows temporary {sample.Status} to author",
                await SearchContainsAsync(sample.Id, authorId, false), null);
            check($"PostgreSQL search shows temporary {sample.Status} to admin",
                await SearchContainsAsync(sample.Id, "verification-admin", true), null);
            check($"PostgreSQL list hides temporary {sample.Status} from guest",
                !await ListContainsAsync(sample.Id, null, false), null);
            check($"PostgreSQL list shows temporary {sample.Status} to author",
                await ListContainsAsync(sample.Id, authorId, false), null);
            check($"PostgreSQL list shows temporary {sample.Status} to admin",
                await ListContainsAsync(sample.Id, "verification-admin", true), null);
        }

        await transaction.RollbackAsync();
        check("temporary PostgreSQL visibility data rolled back", true, null);
    }
    catch (Exception exception)
    {
        await transaction.RollbackAsync();
        check("temporary PostgreSQL visibility verification", false,
            $"{exception.GetType().Name}: {exception.Message}");
    }
}

static async Task<long> CountMatchesAsync(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    string query)
{
    await using var command = new NpgsqlCommand("""
        SELECT COUNT(*) FROM "Recipes"
        WHERE "SearchVector" @@ to_tsquery('simple', unaccent(@query))
        """, connection, transaction);
    command.Parameters.AddWithValue("query", query);
    return (long)(await command.ExecuteScalarAsync())!;
}

static async Task VerifyTemporaryVietnameseRecipeApiAsync(
    string connectionString,
    string apiBaseUrl,
    Action<string, bool, string?> check)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    var recipeId = Guid.NewGuid();
    var token = Guid.NewGuid().ToString("N")[..10];
    var initialTitle = $"Bánh mì exold{token}";
    var updatedTitle = $"Bún chả newtoken{token}";
    var inserted = false;

    try
    {
        Guid categoryId;
        string authorId;
        await using (var source = new NpgsqlCommand("""
            SELECT "CategoryId", "AuthorId" FROM "Recipes"
            WHERE NOT "IsDeleted" AND "Status" = 1
            LIMIT 1
            """, connection))
        await using (var reader = await source.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                check("temporary recipe source", false, "no published recipe is available for references");
                return;
            }
            categoryId = reader.GetGuid(0);
            authorId = reader.GetString(1);
        }

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO "Recipes"
                ("Id", "Title", "Slug", "Description", "PrepTimeMinutes", "CookTimeMinutes",
                 "Servings", "Difficulty", "Status", "CategoryId", "AuthorId", "CreatedAt",
                 "IsDeleted", "RowVersion")
            VALUES
                (@id, @title, @slug, @description, 5, 20, 3, 'Easy', 1, @categoryId, @authorId,
                 now(), false, decode('00', 'hex'))
            """, connection))
        {
            insert.Parameters.AddWithValue("id", recipeId);
            insert.Parameters.AddWithValue("title", initialTitle);
            insert.Parameters.AddWithValue("slug", $"search-verify-{token}");
            insert.Parameters.AddWithValue("description", "Bánh mì kiểm thử FTS và trigger vector.");
            insert.Parameters.AddWithValue("categoryId", categoryId);
            insert.Parameters.AddWithValue("authorId", authorId);
            await insert.ExecuteNonQueryAsync();
            inserted = true;
        }

        using var client = new HttpClient { BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/") };
        async Task<HashSet<Guid>?> SearchIdsAsync(string query)
        {
            using var response = await client.GetAsync(
                "api/v1/recipes/search?q=" + Uri.EscapeDataString(query) + "&page=1&pageSize=50");
            if (!response.IsSuccessStatusCode)
            {
                check("temporary recipe API status", false, $"HTTP {(int)response.StatusCode}");
                return null;
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToHashSet();
        }

        var accented = await SearchIdsAsync($"Bánh mì exold{token}");
        var unaccented = await SearchIdsAsync($"banh mi exold{token}");
        check("live API finds Vietnamese title with accents", accented?.Contains(recipeId) == true, null);
        check("live API finds Vietnamese title without accents", unaccented?.Contains(recipeId) == true, null);

        var filterQuery = $"&categoryId={categoryId:D}&difficulty=Easy&maxCookTime=20&minServings=3";
        using (var filteredResponse = await client.GetAsync("api/v1/recipes/?search=" +
            Uri.EscapeDataString($"banh mi exold{token}") + "&page=1&pageSize=50" + filterQuery))
        {
            using var document = JsonDocument.Parse(await filteredResponse.Content.ReadAsStringAsync());
            var contains = document.RootElement.GetProperty("items").EnumerateArray()
                .Any(item => item.GetProperty("id").GetGuid() == recipeId);
            check("live API combines Vietnamese search and all filters",
                filteredResponse.IsSuccessStatusCode && contains, $"HTTP {(int)filteredResponse.StatusCode}");
        }

        await using (var update = new NpgsqlCommand("""
            UPDATE "Recipes" SET "Title" = @title, "Description" = @description WHERE "Id" = @id
            """, connection))
        {
            update.Parameters.AddWithValue("id", recipeId);
            update.Parameters.AddWithValue("title", updatedTitle);
            update.Parameters.AddWithValue("description", "Bún chả dùng để kiểm tra trigger vector.");
            await update.ExecuteNonQueryAsync();
        }

        var oldTerms = await SearchIdsAsync($"banh mi exold{token}");
        var newTerms = await SearchIdsAsync($"bun cha newtoken{token}");
        check("trigger removes old title terms", oldTerms?.Contains(recipeId) == false, null);
        check("trigger indexes updated Vietnamese title", newTerms?.Contains(recipeId) == true, null);
    }
    catch (Exception exception)
    {
        check("temporary Vietnamese recipe API verification", false,
            $"{exception.GetType().Name}: {exception.Message}");
    }
    finally
    {
        if (inserted)
        {
            try
            {
                await using var delete = new NpgsqlCommand("DELETE FROM \"Recipes\" WHERE \"Id\" = @id", connection);
                delete.Parameters.AddWithValue("id", recipeId);
                await delete.ExecuteNonQueryAsync();
                Console.WriteLine("CLEANUP temporary recipe removed.");
            }
            catch (Exception cleanupException)
            {
                check("temporary recipe cleanup", false,
                    $"{cleanupException.GetType().Name}: {cleanupException.Message}; recipeId={recipeId}");
            }
        }
    }
}

static string RemoveDiacritics(string value)
{
    var decomposed = value.Normalize(NormalizationForm.FormD);
    var result = new StringBuilder(decomposed.Length);
    foreach (var character in decomposed)
    {
        if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
            System.Globalization.UnicodeCategory.NonSpacingMark)
        {
            result.Append(character);
        }
    }
    return result.ToString().Normalize(NormalizationForm.FormC);
}

file sealed class VerificationCurrentUser : ICurrentUserService
{
    public VerificationCurrentUser(string? userId = null, bool isAdmin = false)
    {
        UserId = userId;
        IsAdmin = isAdmin;
    }

    public string? UserId { get; }
    public bool IsAdmin { get; }
    public bool IsAuthenticated => UserId is not null;
}
