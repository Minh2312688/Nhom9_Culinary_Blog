namespace CulinaryBlog.DataMigration;

public sealed record CategoryImportDefaults(string? ImageUrl, int OrderIndex, bool IsDeleted);

public static class ImportMappings
{
    public static CategoryImportDefaults GetCategoryDefaults() => new(null, 0, false);

    public static bool AreEquivalent(object? source, object? target)
    {
        if (source is DBNull)
            source = null;
        if (target is DBNull)
            target = null;
        if (source is null || target is null)
            return source is null && target is null;

        if (source is byte[] sourceBytes && target is byte[] targetBytes)
            return sourceBytes.AsSpan().SequenceEqual(targetBytes);

        if (source is DateTime sourceDateTime && target is DateTimeOffset targetDateTimeOffset)
            return AsUtc(sourceDateTime).Ticks == targetDateTimeOffset.UtcTicks;
        if (source is DateTimeOffset sourceDateTimeOffset && target is DateTime targetDateTime)
            return sourceDateTimeOffset.UtcTicks == AsUtc(targetDateTime).Ticks;

        if (source is DateTime sourceTime && target is DateTime targetTime)
            return AsUtc(sourceTime).Ticks == AsUtc(targetTime).Ticks;
        if (source is DateTimeOffset sourceOffset && target is DateTimeOffset targetOffset)
            return sourceOffset.UtcTicks == targetOffset.UtcTicks;

        return Equals(source, target);
    }

    public static int ParseDifficulty(string? value)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, "Easy", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (string.Equals(normalized, "Medium", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (string.Equals(normalized, "Hard", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(normalized, "Expert", StringComparison.OrdinalIgnoreCase))
            return 4;

        throw new InvalidOperationException("Source contains an unsupported recipe difficulty value.");
    }

    public static string NormalizeRole(string? value)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, "Author", StringComparison.OrdinalIgnoreCase))
            return "Author";
        if (string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase))
            return "Admin";

        throw new InvalidOperationException("Source contains an unsupported user role value.");
    }

    public static void EnsureSeparateDatabases(string sourceConnectionString, string targetConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetConnectionString);

        var sourceDatabase = new Npgsql.NpgsqlConnectionStringBuilder(sourceConnectionString).Database;
        var targetDatabase = new Npgsql.NpgsqlConnectionStringBuilder(targetConnectionString).Database;
        if (string.IsNullOrWhiteSpace(sourceDatabase) || string.IsNullOrWhiteSpace(targetDatabase))
            throw new InvalidOperationException("Both connection strings must specify a database name.");

        if (string.Equals(sourceDatabase, targetDatabase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and target must use different database names.");
    }

    public static void EnsureBackupVerified(string? value)
    {
        if (!string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Before importing, verify a source backup and set CULINARY_BLOG_SOURCE_BACKUP_VERIFIED=yes.");
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
