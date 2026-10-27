using System.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Persistence;

public static class DockerPostgresEnvironment
{
    private static readonly Lazy<bool> dockerAvailable = new(CheckDockerAvailability);

    public static bool IsDockerAvailable => dockerAvailable.Value;

    private static bool CheckDockerAvailability()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null) return false;
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static PostgreSqlBuilder CreatePostgresBuilder()
    {
        return new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("culinary_blog_test")
            .WithUsername("postgres")
            .WithPassword("postgres");
    }
}

public sealed class DockerPostgresFactAttribute : FactAttribute
{
    public DockerPostgresFactAttribute()
    {
        if (!DockerPostgresEnvironment.IsDockerAvailable)
        {
            Skip = "Docker daemon is not running. Start Docker Desktop to run Testcontainers PostgreSQL integration tests.";
        }
    }
}
