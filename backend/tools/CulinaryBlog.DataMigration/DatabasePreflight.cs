using System.Data;
using Npgsql;

namespace CulinaryBlog.DataMigration;

public sealed record PreflightReport(IReadOnlyDictionary<string, long> SourceCounts)
{
    public void PrintSummary()
    {
        Console.WriteLine("Preflight passed. Source row counts:");
        foreach (var (table, count) in SourceCounts.OrderBy(x => x.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {table}: {count}");
        Console.WriteLine("Target schema is migrated and empty for all imported tables.");
    }
}

public static class DatabasePreflight
{
    private static readonly (string Table, string[] Columns)[] RequiredSourceColumns =
    [
        ("ApplicationUser", ["Id", "Role", "DisplayName", "AvatarUrl", "Bio", "IsActive", "CreatedAt", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount"]),
        ("Categories", ["Id", "Name", "Slug", "Description", "CreatedAt", "UpdatedAt"]),
        ("Recipes", ["Id", "Title", "Slug", "Description", "CategoryId", "AuthorId", "PrepTimeMinutes", "CookTimeMinutes", "Servings", "Difficulty", "Status", "CreatedAt", "UpdatedAt", "IsDeleted"]),
        ("RecipeIngredients", ["Id", "RecipeId", "Name", "Quantity", "Unit", "Notes", "OrderIndex", "CreatedAt", "UpdatedAt", "IsDeleted"]),
        ("RecipeSteps", ["Id", "RecipeId", "StepNumber", "Title", "Description", "DurationMinutes", "ImageUrl", "CreatedAt", "UpdatedAt", "IsDeleted"]),
        ("RecipeImages", ["Id", "RecipeId", "OriginalUrl", "MediumUrl", "ThumbnailUrl", "AltText", "IsPrimary", "OrderIndex", "CreatedAt", "UpdatedAt", "IsDeleted"]),
        ("RecipeNutritions", ["Id", "RecipeId", "Calories", "Protein", "Carbohydrates", "Fat", "Fiber", "Sodium", "CreatedAt", "UpdatedAt", "IsDeleted"])
    ];

    internal static readonly string[] ImportTables =
    [
        "AspNetRoles", "AspNetRoleClaims", "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins",
        "AspNetUserRoles", "AspNetUserTokens", "RefreshTokens", "Categories", "Recipes",
        "RecipeIngredients", "RecipeSteps", "RecipeImages", "RecipeNutritions"
    ];

    private static readonly string[] RequiredAuthMigrations =
    [
        "20260921191147_Lab02PersonalInitialDatabase",
        "20260928050453_AlignAuthDbContextRecipeSchema",
        "20260928142200_AddPostgresRowVersionTriggers"
    ];

    public static async Task<PreflightReport> ValidateAsync(
        string sourceConnectionString,
        string targetConnectionString,
        CancellationToken cancellationToken = default)
    {
        ImportMappings.EnsureSeparateDatabases(sourceConnectionString, targetConnectionString);

        var sourceCounts = await ValidateSourceAsync(sourceConnectionString, cancellationToken);
        await ValidateTargetAsync(targetConnectionString, cancellationToken);
        return new PreflightReport(sourceCounts);
    }

    private static async Task<Dictionary<string, long>> ValidateSourceAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);

        await RequireMigrationHistoryAsync(connection, transaction, cancellationToken);
        await RequireSourceColumnsAsync(connection, transaction, cancellationToken);
        await ValidateMappedValuesAsync(connection, transaction, cancellationToken);
        await ValidateConstraintsAsync(connection, transaction, cancellationToken);

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (table, _) in RequiredSourceColumns)
            counts[table] = await CountAsync(connection, transaction, table, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return counts;
    }

    private static async Task RequireMigrationHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var migrations = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            migrations.Add(reader.GetString(0));

        if (!migrations.Contains("20260921083055_InitialRecipeSchema") ||
            !migrations.Contains("20260921101739_AddRowVersionDefaults"))
        {
            throw new InvalidOperationException("Source database does not have the expected ApplicationDbContext migration history.");
        }
    }

    private static async Task RequireSourceColumnsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = @column
            """;

        foreach (var (table, columns) in RequiredSourceColumns)
        foreach (var column in columns)
        {
            await using var command = new NpgsqlCommand(query, connection, transaction);
            command.Parameters.AddWithValue("table", table);
            command.Parameters.AddWithValue("column", column);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new InvalidOperationException($"Source schema is missing required column {table}.{column}.");
        }
    }

    private static async Task ValidateMappedValuesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using (var roles = new NpgsqlCommand("SELECT DISTINCT \"Role\" FROM \"ApplicationUser\"", connection, transaction))
        await using (var reader = await roles.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                _ = ImportMappings.NormalizeRole(reader.IsDBNull(0) ? null : reader.GetString(0));
        }

        await using (var difficulties = new NpgsqlCommand("SELECT DISTINCT \"Difficulty\" FROM \"Recipes\"", connection, transaction))
        await using (var reader = await difficulties.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                _ = ImportMappings.ParseDifficulty(reader.IsDBNull(0) ? null : reader.GetString(0));
        }

        await EnsureNoRowsAsync(connection, transaction, """
            SELECT 1 FROM "ApplicationUser"
            WHERE "NormalizedUserName" IS NOT NULL
            GROUP BY "NormalizedUserName" HAVING COUNT(*) > 1 LIMIT 1
            """, "Source contains duplicate normalized usernames.", cancellationToken);

    }

    private static async Task ValidateConstraintsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        (string Sql, string Error)[] orphanChecks =
        [
            ("SELECT 1 FROM \"Recipes\" r LEFT JOIN \"ApplicationUser\" u ON u.\"Id\" = r.\"AuthorId\" WHERE u.\"Id\" IS NULL LIMIT 1", "A recipe references a missing author."),
            ("SELECT 1 FROM \"Recipes\" r LEFT JOIN \"Categories\" c ON c.\"Id\" = r.\"CategoryId\" WHERE c.\"Id\" IS NULL LIMIT 1", "A recipe references a missing category."),
            ("SELECT 1 FROM \"RecipeIngredients\" x LEFT JOIN \"Recipes\" r ON r.\"Id\" = x.\"RecipeId\" WHERE r.\"Id\" IS NULL LIMIT 1", "An ingredient references a missing recipe."),
            ("SELECT 1 FROM \"RecipeSteps\" x LEFT JOIN \"Recipes\" r ON r.\"Id\" = x.\"RecipeId\" WHERE r.\"Id\" IS NULL LIMIT 1", "A step references a missing recipe."),
            ("SELECT 1 FROM \"RecipeImages\" x LEFT JOIN \"Recipes\" r ON r.\"Id\" = x.\"RecipeId\" WHERE r.\"Id\" IS NULL LIMIT 1", "An image references a missing recipe."),
            ("SELECT 1 FROM \"RecipeNutritions\" x LEFT JOIN \"Recipes\" r ON r.\"Id\" = x.\"RecipeId\" WHERE r.\"Id\" IS NULL LIMIT 1", "Nutrition references a missing recipe."),
            ("SELECT 1 FROM \"RecipeNutritions\" GROUP BY \"RecipeId\" HAVING COUNT(*) > 1 LIMIT 1", "Source has multiple nutrition rows for a recipe."),
            ("SELECT 1 FROM \"RecipeSteps\" WHERE \"IsDeleted\" = false GROUP BY \"RecipeId\", \"StepNumber\" HAVING COUNT(*) > 1 LIMIT 1", "Source has duplicate active step numbers."),
            ("SELECT 1 FROM \"RecipeIngredients\" WHERE \"Quantity\" IS NOT NULL AND abs(\"Quantity\") >= 10000000 LIMIT 1", "An ingredient quantity exceeds target precision."),
            ("SELECT 1 FROM \"Recipes\" WHERE length(\"Slug\") > 220 LIMIT 1", "A recipe slug exceeds target length."),
            ("SELECT 1 FROM \"Recipes\" WHERE length(\"Description\") > 2000 LIMIT 1", "A recipe description exceeds target length."),
            ("SELECT 1 FROM \"Categories\" WHERE length(\"Name\") > 100 OR length(\"Slug\") > 120 OR length(\"Description\") > 2000 LIMIT 1", "A category field exceeds target length."),
            ("SELECT 1 FROM \"RecipeIngredients\" WHERE length(\"Name\") > 200 OR length(\"Unit\") > 50 OR length(\"Notes\") > 500 LIMIT 1", "An ingredient field exceeds target length."),
            ("SELECT 1 FROM \"RecipeSteps\" WHERE length(\"Title\") > 200 OR length(\"ImageUrl\") > 500 LIMIT 1", "A step field exceeds target length."),
            ("SELECT 1 FROM \"RecipeImages\" WHERE length(\"OriginalUrl\") > 500 OR length(\"MediumUrl\") > 500 OR length(\"ThumbnailUrl\") > 500 OR length(\"AltText\") > 200 LIMIT 1", "An image field exceeds target length."),
            ("SELECT 1 FROM \"ApplicationUser\" WHERE length(\"UserName\") > 256 OR length(\"NormalizedUserName\") > 256 OR length(\"Email\") > 256 OR length(\"NormalizedEmail\") > 256 LIMIT 1", "An Identity user field exceeds target length."),
            ("SELECT 1 FROM \"ApplicationUser\" WHERE \"DisplayName\" IS NULL LIMIT 1", "A source user has no display name required by the target schema.")
        ];

        foreach (var (sql, error) in orphanChecks)
            await EnsureNoRowsAsync(connection, transaction, sql, error, cancellationToken);
    }

    private static async Task ValidateTargetAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string migrationsSql = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"";
        var migrations = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(migrationsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                migrations.Add(reader.GetString(0));
        }

        foreach (var migration in RequiredAuthMigrations)
        {
            if (!migrations.Contains(migration))
                throw new InvalidOperationException("Target database is missing a required AuthDbContext migration.");
        }

        foreach (var table in ImportTables)
        {
            await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", connection);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0)
                throw new InvalidOperationException("Target database contains business rows; refusing to import.");
        }
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", connection, transaction);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task EnsureNoRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string error,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
            throw new InvalidOperationException(error);
    }
}
