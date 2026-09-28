using Npgsql;

namespace CulinaryBlog.DataMigration;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 1 || args.Any(arg => arg is not "--apply" and not "--help"))
        {
            Console.Error.WriteLine("Usage: dotnet run --project backend/tools/CulinaryBlog.DataMigration [--apply]");
            return 2;
        }

        if (args.Contains("--help", StringComparer.Ordinal))
        {
            Console.WriteLine("Default mode validates source/target and performs no writes. Pass --apply to import into an empty, migrated target.");
            Console.WriteLine("Set ConnectionStrings__PostgresSource and ConnectionStrings__PostgresTarget in the environment.");
            return 0;
        }

        var source = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresSource");
        var target = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresTarget");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            Console.Error.WriteLine("Set both ConnectionStrings__PostgresSource and ConnectionStrings__PostgresTarget.");
            return 2;
        }

        try
        {
            if (args.Contains("--apply", StringComparer.Ordinal))
            {
                ImportMappings.EnsureBackupVerified(Environment.GetEnvironmentVariable("CULINARY_BLOG_SOURCE_BACKUP_VERIFIED"));
                await DataImporter.ImportAsync(source, target);
                return 0;
            }

            var report = await DatabasePreflight.ValidateAsync(source, target);
            report.PrintSummary();
            Console.WriteLine("Dry run only; no rows were written. Pass --apply to import.");
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"Preflight/import stopped: {exception.Message}");
            return 1;
        }
        catch (PostgresException exception)
        {
            Console.Error.WriteLine($"PostgreSQL operation failed (SQLSTATE {exception.SqlState}); details are omitted to avoid exposing connection or row data.");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Operation failed ({exception.GetType().Name}); details are omitted to avoid exposing connection or row data.");
            return 1;
        }
    }
}
