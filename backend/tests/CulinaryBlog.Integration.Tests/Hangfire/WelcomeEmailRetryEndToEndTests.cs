using System.Diagnostics;
using System.Linq.Expressions;
using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Hangfire;

/// <summary>
/// FR-JOB-001 E2E retry proof (SRS-AUDIT sec-12-4): runs the REAL WelcomeEmailJob
/// through a real BackgroundJobServer on isolated PostgreSQL, with the real
/// SmtpEmailSender pointed at a deliberately unreachable SMTP endpoint
/// (127.0.0.1:1 - nothing listens there, so every send fails with a genuine
/// connection error, not a mocked one). Asserts the production retry contract:
/// exactly 5 executions total (1 initial + 4 retries, each scheduled with a
/// "Retry attempt N of 4" reason), then the job STAYS in Failed - never
/// Succeeded, never Deleted - both live and after worker shutdown.
///
/// The production retry policy is NOT modified. Only the server's schedule
/// poll interval is tightened (a test-only server option, not a retry setting).
///
/// Requires env var HANGFIRE_PERSISTENCE_CONNECTION pointing to an isolated
/// PostgreSQL instance; skipped otherwise so it never touches shared data.
/// </summary>
[Collection("HangfirePersistence")]
public sealed class WelcomeEmailRetryEndToEndTests
{
    private const int ExpectedExecutions = 5; // 1 initial run + 4 retries
    private const int ExpectedRetries = 4;

    [HangfirePersistenceFact]
    public async Task WelcomeEmail_WithDeliberatelyFailingSmtp_Runs5TimesTotal_ThenStaysFailed()
    {
        var connectionString = HangfirePersistenceTestEnvironment.ConnectionString!;
        var storage = CreateStorage(connectionString);
        string jobId;

        // Phase 1 - real worker + real failing SMTP sender.
        using (var server = new BackgroundJobServer(
            new BackgroundJobServerOptions
            {
                ServerName = $"hf-welcome-retry-{Guid.NewGuid():N}",
                SchedulePollingInterval = TimeSpan.FromSeconds(1),
                Activator = new FailingSmtpJobActivator(),
            },
            storage))
        {
            JobStorage.Current = storage;

            var client = new BackgroundJobClient(storage);
            Expression<Action<WelcomeEmailJob>> method = job =>
                job.SendWelcomeEmailAsync("e2e-retry-user", "nobody@example.test", "Retry Tester");
            jobId = client.Enqueue(method);
            jobId.Should().NotBeNullOrWhiteSpace();

            var monitoringApi = storage.GetMonitoringApi();
            await WaitUntilFailedAsync(monitoringApi, jobId);

            AssertRetryContract(monitoringApi.JobDetails(jobId), "live worker");
        }
        // Server disposed: phase 2 reads only persisted PostgreSQL state.

        var verifyStorage = CreateStorage(connectionString);
        AssertRetryContract(
            verifyStorage.GetMonitoringApi().JobDetails(jobId),
            "persisted state after worker shutdown");
    }

    private static void AssertRetryContract(JobDetailsDto? details, string phase)
    {
        details.Should().NotBeNull(phase);
        var history = details!.History;
        history.Should().NotBeEmpty(phase);

        history[0].StateName.Should().Be(FailedState.StateName,
            $"{phase}: the terminal state must be Failed");

        history.Count(h => h.StateName == ProcessingState.StateName).Should().Be(ExpectedExecutions,
            $"{phase}: 1 initial run + 4 retries must produce exactly {ExpectedExecutions} executions");

        history.Count(h => h.StateName == ScheduledState.StateName).Should().Be(ExpectedRetries,
            $"{phase}: each of the {ExpectedRetries} retries must be scheduled exactly once");

        history.Count(h => h.StateName == SucceededState.StateName).Should().Be(0,
            $"{phase}: the SMTP endpoint always fails, so the job must never succeed");

        history.Count(h => h.StateName == DeletedState.StateName).Should().Be(0,
            $"{phase}: OnAttemptsExceeded=Fail must keep the job instead of deleting it");

        var retryReasons = history
            .Where(h => h.StateName == ScheduledState.StateName)
            .Select(h => h.Reason ?? string.Empty)
            .ToList();
        for (var attempt = 1; attempt <= ExpectedRetries; attempt++)
        {
            retryReasons.Should().Contain(
                r => r.StartsWith($"Retry attempt {attempt} of {ExpectedRetries}", StringComparison.Ordinal),
                $"{phase}: retry {attempt} must have been scheduled by the production AutomaticRetry policy");
        }

        var failed = history[0];
        failed.Data.Any(d =>
                d.Key.Contains("Exception", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(d.Value))
            .Should().BeTrue($"{phase}: the Failed state must record the real SMTP exception");
    }

    private static async Task WaitUntilFailedAsync(IMonitoringApi monitoringApi, string jobId)
    {
        // Production backoff (Hangfire default exponential + jitter) for 4 retries
        // measures up to ~436s; 15 minutes leaves ample margin.
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(15))
        {
            var details = monitoringApi.JobDetails(jobId);
            if (details is { History.Count: > 0 } && details.History[0].StateName == FailedState.StateName)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException(
            $"Welcome email job {jobId} did not reach the Failed state within 15 minutes.");
    }

    private static JobStorage CreateStorage(string connectionString)
    {
        var options = new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(1)
        };

        return new PostgreSqlStorage(
            new NpgsqlConnectionFactory(connectionString, options, _ => { }),
            options);
    }

    /// <summary>
    /// Activator that builds the REAL WelcomeEmailJob wired to the REAL
    /// SmtpEmailSender, configured with an SMTP endpoint that is guaranteed
    /// closed (port 1 on loopback) so every send attempt fails genuinely.
    /// </summary>
    private sealed class FailingSmtpJobActivator : JobActivator
    {
        public override object ActivateJob(Type jobType)
        {
            if (jobType == typeof(WelcomeEmailJob))
            {
                var smtpOptions = new SmtpOptions
                {
                    Host = "127.0.0.1",
                    Port = 1,
                    UseSsl = false,
                    FromAddress = "no-reply@e2e.test",
                    FromName = "TV4 E2E"
                };

                var sender = new SmtpEmailSender(
                    Options.Create(smtpOptions),
                    NullLogger<SmtpEmailSender>.Instance);

                return new WelcomeEmailJob(sender, NullLogger<WelcomeEmailJob>.Instance);
            }

            return base.ActivateJob(jobType);
        }
    }
}
