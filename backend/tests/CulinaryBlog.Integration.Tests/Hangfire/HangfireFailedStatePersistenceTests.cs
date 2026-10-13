using System.Diagnostics;
using System.Linq.Expressions;
using FluentAssertions;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.Server;
using Hangfire.Storage;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Hangfire;

/// <summary>
/// Deterministic integration test: proves the Hangfire Failed state of a job
/// executed by a real BackgroundJobServer is persisted in a real PostgreSQL
/// storage and remains Failed after the worker/storage connections are
/// disposed and a NEW JobStorage instance is created against the same
/// PostgreSQL instance (simulating worker/API restart: nothing is kept in
/// process memory between the two phases).
///
/// A test-only job that always throws, with a test-only retry policy
/// (Attempts = 0), enters Failed immediately. The production WelcomeEmailJob
/// policy (Attempts = 4) is NOT touched by this test.
///
/// LIMITATION (do not over-claim): this test proves Hangfire Failed-state
/// persistence in PostgreSQL storage for a failing job. It does NOT prove
/// that WelcomeEmailJob traverses its full 4-retry path - that would require
/// a separate deterministic run of the production job.
///
/// Requires env var HANGFIRE_PERSISTENCE_CONNECTION pointing to an isolated
/// PostgreSQL instance (dedicated container); skipped otherwise so it never
/// touches shared data.
/// </summary>
[Collection("HangfirePersistence")]
public sealed class HangfireFailedStatePersistenceTests
{
    private const string Schema = "hangfire";

    [HangfirePersistenceFact]
    public async Task FailedJob_ShouldPersistInPostgreSqlStorage_AndRemainFailed_AfterWorkerRestart()
    {
        var connectionString = HangfirePersistenceTestEnvironment.ConnectionString!;

        string jobId;

        // Phase 1 - "worker run 1": real PostgreSQL storage + real server.
        // Enqueue a test-only always-failing job; wait until it is Failed.
        // (JobStorage is not IDisposable in Hangfire 1.8; the server is the
        // disposable worker component. Nothing in-process caches job state,
        // so phase 2 can only read persisted PostgreSQL data.)
        var storage = CreateStorage(connectionString);
        using (var server = new BackgroundJobServer(
            new BackgroundJobServerOptions { ServerName = "hf-persistence-test-worker-1" },
            storage))
        {
            JobStorage.Current = storage;

            var client = new BackgroundJobClient(storage);
            Expression<Action<AlwaysFailingTestJob>> method = job => job.ThrowNow();
            jobId = client.Enqueue(method);

            jobId.Should().NotBeNullOrWhiteSpace();

            var monitoringApi = storage.GetMonitoringApi();
            await WaitUntilFailedAsync(monitoringApi, jobId);

            // Confirm via Hangfire monitoring API before shutdown.
            var failedBeforeRestart = monitoringApi.JobDetails(jobId);
            failedBeforeRestart.Should().NotBeNull();
            failedBeforeRestart!.History[0].StateName.Should().Be("Failed");
        }
        // Server is disposed here: the worker stopped.

        // Phase 2 - "restart": brand-new JobStorage instance (fresh connection
        // pool) against the SAME isolated PostgreSQL. Any state we read now
        // can only come from the persisted database.
        var restartedStorage = CreateStorage(connectionString);
        var restartedMonitoringApi = restartedStorage.GetMonitoringApi();
        var detailsAfterRestart = restartedMonitoringApi.JobDetails(jobId);

            detailsAfterRestart.Should().NotBeNull(
                "the job must still exist in PostgreSQL storage after the worker restarted");
            detailsAfterRestart!.History[0].StateName.Should().Be("Failed",
                "the Failed state must survive worker/API restart");
            detailsAfterRestart.History[0].Data.Keys.Should().Contain(
                key => key.Contains("Exception", StringComparison.Ordinal),
                "the failure details (exception) must be persisted with the Failed state");

        // Phase 3 - raw SQL against the actual schema (schema verified via
        // information_schema first; table/column names are not assumed).
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var listTables = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema ORDER BY table_name",
            connection))
        {
            listTables.Parameters.AddWithValue("schema", Schema);
            await using var reader = await listTables.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        tables.Should().Contain("job", "Hangfire PostgreSQL storage must contain the job table");
        tables.Should().Contain("state", "Hangfire PostgreSQL storage must contain the state table");

        await using var queryState = new NpgsqlCommand(
            "SELECT s.name FROM hangfire.job j " +
            "JOIN hangfire.state s ON s.jobid = j.id " +
            "AND s.id = (SELECT max(id) FROM hangfire.state s2 WHERE s2.jobid = j.id) " +
            "WHERE j.id = @id::bigint",
            connection);
        queryState.Parameters.AddWithValue("id", jobId);
        var persistedState = (string?)await queryState.ExecuteScalarAsync();

        persistedState.Should().Be("Failed",
            "the latest persisted state row for the job must be Failed");
    }

    private static JobStorage CreateStorage(string connectionString)
    {
        var options = new PostgreSqlStorageOptions
        {
            SchemaName = Schema,
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(1)
        };

        // New (non-obsolete) storage construction: connection factory overload.
        return new PostgreSqlStorage(
            new NpgsqlConnectionFactory(connectionString, options, _ => { }),
            options);
    }

    private static async Task WaitUntilFailedAsync(IMonitoringApi monitoringApi, string jobId)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
        {
            var details = monitoringApi.JobDetails(jobId);
            if (details is { History.Count: > 0 } && details.History[0].StateName == "Failed")
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Job {jobId} did not reach the Failed state within 60 seconds.");
    }

    /// <summary>
    /// Test-only job: always throws. The Attempts = 0 policy below is
    /// test-only so the job enters Failed on the first failure; it does not
    /// represent or modify the production WelcomeEmailJob retry policy.
    /// </summary>
    private sealed class AlwaysFailingTestJob
    {
        [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail, LogEvents = true)]
        public Task ThrowNow() =>
            throw new InvalidOperationException("deterministic persistence-test failure");
    }
}

/// <summary>
/// Environment gate for the persistence test: only runs when an isolated
/// PostgreSQL connection string is provided via HANGFIRE_PERSISTENCE_CONNECTION.
/// </summary>
internal static class HangfirePersistenceTestEnvironment
{
    public const string ConnectionVariable = "HANGFIRE_PERSISTENCE_CONNECTION";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } raw
            ? raw.Trim()
            : null;

    public static string SkipReason =>
        $"Set {ConnectionVariable} to an isolated PostgreSQL instance to run this test " +
        "(never a shared database).";
}

/// <summary>
/// Fact that skips (never silently passes) when the isolated PostgreSQL
/// instance is not configured.
/// </summary>
public sealed class HangfirePersistenceFactAttribute : FactAttribute
{
    public HangfirePersistenceFactAttribute()
    {
        if (HangfirePersistenceTestEnvironment.ConnectionString is null)
        {
            Skip = HangfirePersistenceTestEnvironment.SkipReason;
        }
    }
}
