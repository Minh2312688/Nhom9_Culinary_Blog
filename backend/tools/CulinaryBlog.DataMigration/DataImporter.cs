using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace CulinaryBlog.DataMigration;

public static class DataImporter
{
    private const string UserColumns = "\"Id\", \"DisplayName\", \"AvatarUrl\", \"Bio\", \"IsActive\", \"CreatedAt\", \"UserName\", \"NormalizedUserName\", \"Email\", \"NormalizedEmail\", \"EmailConfirmed\", \"PasswordHash\", \"SecurityStamp\", \"ConcurrencyStamp\", \"PhoneNumber\", \"PhoneNumberConfirmed\", \"TwoFactorEnabled\", \"LockoutEnd\", \"LockoutEnabled\", \"AccessFailedCount\"";

    public static async Task ImportAsync(
        string sourceConnectionString,
        string targetConnectionString,
        CancellationToken cancellationToken = default)
    {
        ImportMappings.EnsureBackupVerified(Environment.GetEnvironmentVariable("CULINARY_BLOG_SOURCE_BACKUP_VERIFIED"));
        var preflight = await DatabasePreflight.ValidateAsync(sourceConnectionString, targetConnectionString, cancellationToken);
        preflight.PrintSummary();

        await using var source = new NpgsqlConnection(sourceConnectionString);
        await source.OpenAsync(cancellationToken);
        await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", source, sourceTransaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);

        await using var target = new NpgsqlConnection(targetConnectionString);
        await target.OpenAsync(cancellationToken);
        await using var targetTransaction = await target.BeginTransactionAsync(cancellationToken);

        await LockAndRequireEmptyTargetAsync(target, targetTransaction, cancellationToken);
        var sourceCounts = await GetSourceCountsAsync(source, sourceTransaction, cancellationToken);
        var roleIds = await ImportRolesAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);

        await CopyUsersAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await CopyUserRolesAsync(source, sourceTransaction, target, targetTransaction, roleIds, cancellationToken);
        await CopyCategoriesAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await CopyRecipesAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await CopyIngredientsAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await CopyStepsAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await CopyImagesAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await CopyNutritionAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);

        await VerifyCountsAsync(source, sourceTransaction, target, targetTransaction, sourceCounts, roleIds.Count, cancellationToken);
        await VerifyIdsAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);
        await VerifyDataAsync(source, sourceTransaction, target, targetTransaction, cancellationToken);

        await targetTransaction.CommitAsync(cancellationToken);
        await sourceTransaction.CommitAsync(cancellationToken);
        Console.WriteLine("Import committed. Source database was read-only and was not modified.");
    }

    private static async Task LockAndRequireEmptyTargetAsync(
        NpgsqlConnection target,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tables = string.Join(", ", DatabasePreflight.ImportTables.Select(name => $"\"{name}\""));
        await using (var lockCommand = new NpgsqlCommand($"LOCK TABLE {tables} IN EXCLUSIVE MODE", target, transaction))
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var table in DatabasePreflight.ImportTables)
        {
            var count = await CountAsync(target, transaction, table, cancellationToken);
            if (count != 0)
                throw new InvalidOperationException("Target database contains business rows; refusing to import.");
        }
    }

    private static async Task<Dictionary<string, string>> ImportRolesAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTransaction,
        NpgsqlConnection target,
        NpgsqlTransaction targetTransaction,
        CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("SELECT DISTINCT \"Role\" FROM \"ApplicationUser\"", source, sourceTransaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                names.Add(ImportMappings.NormalizeRole(reader.IsDBNull(0) ? null : reader.GetString(0)));
        }

        var roleIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names.Order(StringComparer.Ordinal))
        {
            var id = Guid.NewGuid().ToString("N");
            await using var insert = new NpgsqlCommand("""
                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES (@id, @name, @normalizedName, @concurrencyStamp)
                """, target, targetTransaction);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("name", name);
            insert.Parameters.AddWithValue("normalizedName", name.ToUpperInvariant());
            insert.Parameters.AddWithValue("concurrencyStamp", Guid.NewGuid().ToString("N"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
            roleIds.Add(name, id);
        }

        return roleIds;
    }

    private static Task CopyUsersAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            $"SELECT {UserColumns} FROM \"ApplicationUser\" ORDER BY \"Id\"",
            $"COPY \"AspNetUsers\" ({UserColumns}) FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                await W(writer, reader.GetString(0), NpgsqlDbType.Text, token);
                await W(writer, reader.GetString(1), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 2), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 3), NpgsqlDbType.Text, token);
                await W(writer, reader.GetBoolean(4), NpgsqlDbType.Boolean, token);
                await W(writer, reader.GetDateTime(5), NpgsqlDbType.TimestampTz, token);
                await W(writer, S(reader, 6), NpgsqlDbType.Varchar, token);
                await W(writer, S(reader, 7), NpgsqlDbType.Varchar, token);
                await W(writer, S(reader, 8), NpgsqlDbType.Varchar, token);
                await W(writer, S(reader, 9), NpgsqlDbType.Varchar, token);
                await W(writer, reader.GetBoolean(10), NpgsqlDbType.Boolean, token);
                await W(writer, S(reader, 11), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 12), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 13), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 14), NpgsqlDbType.Text, token);
                await W(writer, reader.GetBoolean(15), NpgsqlDbType.Boolean, token);
                await W(writer, reader.GetBoolean(16), NpgsqlDbType.Boolean, token);
                await WNullableDateTimeOffset(writer, reader, 17, token);
                await W(writer, reader.GetBoolean(18), NpgsqlDbType.Boolean, token);
                await W(writer, reader.GetInt32(19), NpgsqlDbType.Integer, token);
            }, ct);

    private static Task CopyUserRolesAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTx,
        NpgsqlConnection target,
        NpgsqlTransaction targetTx,
        IReadOnlyDictionary<string, string> roleIds,
        CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"Role\" FROM \"ApplicationUser\" ORDER BY \"Id\"",
            "COPY \"AspNetUserRoles\" (\"UserId\", \"RoleId\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                var role = ImportMappings.NormalizeRole(reader.IsDBNull(1) ? null : reader.GetString(1));
                await W(writer, reader.GetString(0), NpgsqlDbType.Text, token);
                await W(writer, roleIds[role], NpgsqlDbType.Text, token);
            }, ct);

    private static Task CopyCategoriesAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"Name\", \"Slug\", \"Description\", \"CreatedAt\", \"UpdatedAt\" FROM \"Categories\" ORDER BY \"Id\"",
            "COPY \"Categories\" (\"Id\", \"Name\", \"Slug\", \"Description\", \"ImageUrl\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                var defaults = ImportMappings.GetCategoryDefaults();
                await W(writer, reader.GetGuid(0), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetString(1), NpgsqlDbType.Text, token);
                await W(writer, reader.GetString(2), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 3), NpgsqlDbType.Text, token);
                await W(writer, defaults.ImageUrl, NpgsqlDbType.Text, token);
                await W(writer, defaults.OrderIndex, NpgsqlDbType.Integer, token);
                await W(writer, reader.GetDateTime(4), NpgsqlDbType.TimestampTz, token);
                await WNullableDateTime(writer, reader, 5, token);
                await W(writer, defaults.IsDeleted, NpgsqlDbType.Boolean, token);
            }, ct);

    private static Task CopyRecipesAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"Title\", \"Slug\", \"Description\", \"CategoryId\", \"AuthorId\", \"PrepTimeMinutes\", \"CookTimeMinutes\", \"Servings\", \"Difficulty\", \"Status\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"Recipes\" ORDER BY \"Id\"",
            "COPY \"Recipes\" (\"Id\", \"Title\", \"Slug\", \"Description\", \"CategoryId\", \"AuthorId\", \"PrepTimeMinutes\", \"CookTimeMinutes\", \"Servings\", \"Difficulty\", \"Status\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                await W(writer, reader.GetGuid(0), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetString(1), NpgsqlDbType.Text, token);
                await W(writer, reader.GetString(2), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 3), NpgsqlDbType.Text, token);
                await W(writer, reader.GetGuid(4), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetString(5), NpgsqlDbType.Varchar, token);
                await W(writer, reader.GetInt32(6), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetInt32(7), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetInt32(8), NpgsqlDbType.Integer, token);
                await W(writer, ImportMappings.ParseDifficulty(reader.IsDBNull(9) ? null : reader.GetString(9)), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetInt32(10), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetDateTime(11), NpgsqlDbType.TimestampTz, token);
                await WNullableDateTime(writer, reader, 12, token);
                await W(writer, reader.GetBoolean(13), NpgsqlDbType.Boolean, token);
            }, ct);

    private static Task CopyIngredientsAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"RecipeId\", \"Name\", \"Quantity\", \"Unit\", \"Notes\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeIngredients\" ORDER BY \"Id\"",
            "COPY \"RecipeIngredients\" (\"Id\", \"RecipeId\", \"Name\", \"Quantity\", \"Unit\", \"Notes\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                await W(writer, reader.GetGuid(0), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetGuid(1), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetString(2), NpgsqlDbType.Text, token);
                await WNullableDecimal(writer, reader, 3, token);
                await W(writer, S(reader, 4), NpgsqlDbType.Varchar, token);
                await W(writer, S(reader, 5), NpgsqlDbType.Text, token);
                await W(writer, reader.GetInt32(6), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetDateTime(7), NpgsqlDbType.TimestampTz, token);
                await WNullableDateTime(writer, reader, 8, token);
                await W(writer, reader.GetBoolean(9), NpgsqlDbType.Boolean, token);
            }, ct);

    private static Task CopyStepsAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"RecipeId\", \"StepNumber\", \"Title\", \"Description\", \"DurationMinutes\", \"ImageUrl\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeSteps\" ORDER BY \"Id\"",
            "COPY \"RecipeSteps\" (\"Id\", \"RecipeId\", \"StepNumber\", \"Title\", \"Description\", \"DurationMinutes\", \"ImageUrl\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                await W(writer, reader.GetGuid(0), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetGuid(1), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetInt32(2), NpgsqlDbType.Integer, token);
                await W(writer, S(reader, 3), NpgsqlDbType.Text, token);
                await W(writer, reader.GetString(4), NpgsqlDbType.Text, token);
                await WNullableInt(writer, reader, 5, token);
                await W(writer, S(reader, 6), NpgsqlDbType.Text, token);
                await W(writer, reader.GetDateTime(7), NpgsqlDbType.TimestampTz, token);
                await WNullableDateTime(writer, reader, 8, token);
                await W(writer, reader.GetBoolean(9), NpgsqlDbType.Boolean, token);
            }, ct);

    private static Task CopyImagesAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"RecipeId\", \"OriginalUrl\", \"MediumUrl\", \"ThumbnailUrl\", \"AltText\", \"IsPrimary\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeImages\" ORDER BY \"Id\"",
            "COPY \"RecipeImages\" (\"Id\", \"RecipeId\", \"OriginalUrl\", \"MediumUrl\", \"ThumbnailUrl\", \"AltText\", \"IsPrimary\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                await W(writer, reader.GetGuid(0), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetGuid(1), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetString(2), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 3), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 4), NpgsqlDbType.Text, token);
                await W(writer, S(reader, 5), NpgsqlDbType.Text, token);
                await W(writer, reader.GetBoolean(6), NpgsqlDbType.Boolean, token);
                await W(writer, reader.GetInt32(7), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetDateTime(8), NpgsqlDbType.TimestampTz, token);
                await WNullableDateTime(writer, reader, 9, token);
                await W(writer, reader.GetBoolean(10), NpgsqlDbType.Boolean, token);
            }, ct);

    private static Task CopyNutritionAsync(NpgsqlConnection source, NpgsqlTransaction sourceTx, NpgsqlConnection target, NpgsqlTransaction targetTx, CancellationToken ct) =>
        CopyAsync(source, sourceTx, target, targetTx,
            "SELECT \"Id\", \"RecipeId\", \"Calories\", \"Protein\", \"Carbohydrates\", \"Fat\", \"Fiber\", \"Sodium\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeNutritions\" ORDER BY \"Id\"",
            "COPY \"RecipeNutritions\" (\"Id\", \"RecipeId\", \"Calories\", \"Protein\", \"Carbohydrates\", \"Fat\", \"Fiber\", \"Sodium\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") FROM STDIN (FORMAT BINARY)",
            async (reader, writer, token) =>
            {
                await W(writer, reader.GetGuid(0), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetGuid(1), NpgsqlDbType.Uuid, token);
                await W(writer, reader.GetInt32(2), NpgsqlDbType.Integer, token);
                await W(writer, reader.GetDecimal(3), NpgsqlDbType.Numeric, token);
                await W(writer, reader.GetDecimal(4), NpgsqlDbType.Numeric, token);
                await W(writer, reader.GetDecimal(5), NpgsqlDbType.Numeric, token);
                await W(writer, reader.GetDecimal(6), NpgsqlDbType.Numeric, token);
                await W(writer, reader.GetDecimal(7), NpgsqlDbType.Numeric, token);
                await W(writer, reader.GetDateTime(8), NpgsqlDbType.TimestampTz, token);
                await WNullableDateTime(writer, reader, 9, token);
                await W(writer, reader.GetBoolean(10), NpgsqlDbType.Boolean, token);
            }, ct);

    private static async Task CopyAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTransaction,
        NpgsqlConnection target,
        NpgsqlTransaction targetTransaction,
        string selectSql,
        string copySql,
        Func<NpgsqlDataReader, NpgsqlBinaryImporter, CancellationToken, Task> writeRow,
        CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(targetTransaction.Connection, target))
            throw new InvalidOperationException("Target import transaction is not active on the expected connection.");

        await using var command = new NpgsqlCommand(selectSql, source, sourceTransaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await using var importer = await target.BeginBinaryImportAsync(copySql, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            await importer.StartRowAsync(cancellationToken);
            await writeRow(reader, importer, cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
    }

    private static async Task VerifyCountsAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTransaction,
        NpgsqlConnection target,
        NpgsqlTransaction targetTransaction,
        IReadOnlyDictionary<string, long> expected,
        int roleCount,
        CancellationToken cancellationToken)
    {
        var sourceToTarget = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApplicationUser"] = "AspNetUsers",
            ["Categories"] = "Categories",
            ["Recipes"] = "Recipes",
            ["RecipeIngredients"] = "RecipeIngredients",
            ["RecipeSteps"] = "RecipeSteps",
            ["RecipeImages"] = "RecipeImages",
            ["RecipeNutritions"] = "RecipeNutritions"
        };

        foreach (var (sourceTable, targetTable) in sourceToTarget)
        {
            var sourceCount = await CountAsync(source, sourceTransaction, sourceTable, cancellationToken);
            var targetCount = await CountAsync(target, targetTransaction, targetTable, cancellationToken);
            if (sourceCount != targetCount || sourceCount != expected[sourceTable])
                throw new InvalidOperationException("Post-import row counts do not match the source snapshot.");
        }

        if (await CountAsync(target, targetTransaction, "AspNetRoles", cancellationToken) != roleCount ||
            await CountAsync(target, targetTransaction, "AspNetUserRoles", cancellationToken) != expected["ApplicationUser"])
        {
            throw new InvalidOperationException("Post-import Identity role counts do not match source mappings.");
        }
    }

    private static async Task VerifyIdsAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTransaction,
        NpgsqlConnection target,
        NpgsqlTransaction targetTransaction,
        CancellationToken cancellationToken)
    {
        (string Source, string Target)[] pairs =
        [
            ("ApplicationUser", "AspNetUsers"), ("Categories", "Categories"), ("Recipes", "Recipes"),
            ("RecipeIngredients", "RecipeIngredients"), ("RecipeSteps", "RecipeSteps"),
            ("RecipeImages", "RecipeImages"), ("RecipeNutritions", "RecipeNutritions")
        ];

        foreach (var (sourceTable, targetTable) in pairs)
        {
            var sourceIds = await ReadIdsAsync(source, sourceTransaction, sourceTable, cancellationToken);
            var targetIds = await ReadIdsAsync(target, targetTransaction, targetTable, cancellationToken);
            if (!sourceIds.SetEquals(targetIds))
                throw new InvalidOperationException("Post-import IDs do not match the source snapshot.");
        }
    }

    private static async Task VerifyDataAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTransaction,
        NpgsqlConnection target,
        NpgsqlTransaction targetTransaction,
        CancellationToken cancellationToken)
    {
        (string SourceSql, string TargetSql)[] projections =
        [
            ($"SELECT {UserColumns} FROM \"ApplicationUser\" ORDER BY \"Id\"", $"SELECT {UserColumns} FROM \"AspNetUsers\" ORDER BY \"Id\""),
            ("SELECT \"Id\", \"Name\", \"Slug\", \"Description\", NULL::character varying(500), 0::integer, \"CreatedAt\", \"UpdatedAt\", false::boolean FROM \"Categories\" ORDER BY \"Id\"", "SELECT \"Id\", \"Name\", \"Slug\", \"Description\", \"ImageUrl\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"Categories\" ORDER BY \"Id\""),
            ("SELECT \"Id\", \"Title\", \"Slug\", \"Description\", \"CategoryId\", \"AuthorId\", \"PrepTimeMinutes\", \"CookTimeMinutes\", \"Servings\", CASE upper(btrim(\"Difficulty\")) WHEN 'EASY' THEN 1 WHEN 'MEDIUM' THEN 2 WHEN 'HARD' THEN 3 WHEN 'EXPERT' THEN 4 END::integer, \"Status\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"Recipes\" ORDER BY \"Id\"", "SELECT \"Id\", \"Title\", \"Slug\", \"Description\", \"CategoryId\", \"AuthorId\", \"PrepTimeMinutes\", \"CookTimeMinutes\", \"Servings\", \"Difficulty\", \"Status\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"Recipes\" ORDER BY \"Id\""),
            ("SELECT \"Id\", \"RecipeId\", \"Name\", \"Quantity\", \"Unit\", \"Notes\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeIngredients\" ORDER BY \"Id\"", "SELECT \"Id\", \"RecipeId\", \"Name\", \"Quantity\", \"Unit\", \"Notes\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeIngredients\" ORDER BY \"Id\""),
            ("SELECT \"Id\", \"RecipeId\", \"StepNumber\", \"Title\", \"Description\", \"DurationMinutes\", \"ImageUrl\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeSteps\" ORDER BY \"Id\"", "SELECT \"Id\", \"RecipeId\", \"StepNumber\", \"Title\", \"Description\", \"DurationMinutes\", \"ImageUrl\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeSteps\" ORDER BY \"Id\""),
            ("SELECT \"Id\", \"RecipeId\", \"OriginalUrl\", \"MediumUrl\", \"ThumbnailUrl\", \"AltText\", \"IsPrimary\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeImages\" ORDER BY \"Id\"", "SELECT \"Id\", \"RecipeId\", \"OriginalUrl\", \"MediumUrl\", \"ThumbnailUrl\", \"AltText\", \"IsPrimary\", \"OrderIndex\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeImages\" ORDER BY \"Id\""),
            ("SELECT \"Id\", \"RecipeId\", \"Calories\", \"Protein\", \"Carbohydrates\", \"Fat\", \"Fiber\", \"Sodium\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeNutritions\" ORDER BY \"Id\"", "SELECT \"Id\", \"RecipeId\", \"Calories\", \"Protein\", \"Carbohydrates\", \"Fat\", \"Fiber\", \"Sodium\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" FROM \"RecipeNutritions\" ORDER BY \"Id\"")
        ];

        foreach (var (sourceSql, targetSql) in projections)
            await EnsureProjectionMatchesAsync(source, sourceTransaction, sourceSql, target, targetTransaction, targetSql, cancellationToken);

        const string sourceRoles = "SELECT \"Id\", CASE upper(btrim(\"Role\")) WHEN 'AUTHOR' THEN 'Author' WHEN 'ADMIN' THEN 'Admin' END FROM \"ApplicationUser\" ORDER BY \"Id\"";
        const string targetRoles = "SELECT ur.\"UserId\", r.\"Name\" FROM \"AspNetUserRoles\" ur JOIN \"AspNetRoles\" r ON r.\"Id\" = ur.\"RoleId\" ORDER BY ur.\"UserId\"";
        await EnsureProjectionMatchesAsync(source, sourceTransaction, sourceRoles, target, targetTransaction, targetRoles, cancellationToken);
    }

    private static async Task EnsureProjectionMatchesAsync(
        NpgsqlConnection source,
        NpgsqlTransaction sourceTransaction,
        string sourceSql,
        NpgsqlConnection target,
        NpgsqlTransaction targetTransaction,
        string targetSql,
        CancellationToken cancellationToken)
    {
        await using var sourceCommand = new NpgsqlCommand(sourceSql, source, sourceTransaction);
        await using var targetCommand = new NpgsqlCommand(targetSql, target, targetTransaction);
        await using var sourceReader = await sourceCommand.ExecuteReaderAsync(cancellationToken);
        await using var targetReader = await targetCommand.ExecuteReaderAsync(cancellationToken);

        while (true)
        {
            var sourceHasRow = await sourceReader.ReadAsync(cancellationToken);
            var targetHasRow = await targetReader.ReadAsync(cancellationToken);
            if (sourceHasRow != targetHasRow)
                throw new InvalidOperationException("Post-import data rows do not match the source snapshot.");
            if (!sourceHasRow)
                return;

            if (sourceReader.FieldCount != targetReader.FieldCount)
                throw new InvalidOperationException("Post-import data projection does not match the target schema.");

            for (var ordinal = 0; ordinal < sourceReader.FieldCount; ordinal++)
            {
                if (!ImportMappings.AreEquivalent(sourceReader.GetValue(ordinal), targetReader.GetValue(ordinal)))
                    throw new InvalidOperationException("Post-import field values do not match the source mapping.");
            }
        }
    }

    private static async Task<HashSet<string>> ReadIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand($"SELECT \"Id\" FROM \"{table}\" ORDER BY \"Id\"", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetValue(0).ToString()!);
        return ids;
    }

    private static async Task<Dictionary<string, long>> GetSourceCountsAsync(
        NpgsqlConnection source,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in new[] { "ApplicationUser", "Categories", "Recipes", "RecipeIngredients", "RecipeSteps", "RecipeImages", "RecipeNutritions" })
            counts[table] = await CountAsync(source, transaction, table, cancellationToken);
        return counts;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", connection, transaction);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static string? S(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static Task W(NpgsqlBinaryImporter writer, string? value, NpgsqlDbType type, CancellationToken token) =>
        value is null ? writer.WriteNullAsync(token) : writer.WriteAsync(value, type, token);

    private static Task W(NpgsqlBinaryImporter writer, Guid value, NpgsqlDbType type, CancellationToken token) => writer.WriteAsync(value, type, token);
    private static Task W(NpgsqlBinaryImporter writer, int value, NpgsqlDbType type, CancellationToken token) => writer.WriteAsync(value, type, token);
    private static Task W(NpgsqlBinaryImporter writer, bool value, NpgsqlDbType type, CancellationToken token) => writer.WriteAsync(value, type, token);
    private static Task W(NpgsqlBinaryImporter writer, decimal value, NpgsqlDbType type, CancellationToken token) => writer.WriteAsync(value, type, token);
    private static Task W(NpgsqlBinaryImporter writer, DateTime value, NpgsqlDbType type, CancellationToken token) => writer.WriteAsync(value, type, token);

    private static Task WNullableInt(NpgsqlBinaryImporter writer, NpgsqlDataReader reader, int ordinal, CancellationToken token) =>
        reader.IsDBNull(ordinal) ? writer.WriteNullAsync(token) : W(writer, reader.GetInt32(ordinal), NpgsqlDbType.Integer, token);

    private static Task WNullableDecimal(NpgsqlBinaryImporter writer, NpgsqlDataReader reader, int ordinal, CancellationToken token) =>
        reader.IsDBNull(ordinal) ? writer.WriteNullAsync(token) : W(writer, reader.GetDecimal(ordinal), NpgsqlDbType.Numeric, token);

    private static Task WNullableDateTime(NpgsqlBinaryImporter writer, NpgsqlDataReader reader, int ordinal, CancellationToken token) =>
        reader.IsDBNull(ordinal) ? writer.WriteNullAsync(token) : W(writer, reader.GetDateTime(ordinal), NpgsqlDbType.TimestampTz, token);

    private static Task WNullableDateTimeOffset(NpgsqlBinaryImporter writer, NpgsqlDataReader reader, int ordinal, CancellationToken token) =>
        reader.IsDBNull(ordinal) ? writer.WriteNullAsync(token) : writer.WriteAsync(reader.GetFieldValue<DateTimeOffset>(ordinal), NpgsqlDbType.TimestampTz, token);
}
