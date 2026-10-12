using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

/// <summary>
/// Verifies FR-JOB-001 retry contract per SRS-AUDIT sec-12-4 (group-confirmed
/// current policy): at most 5 executions in total - the initial run plus up
/// to 4 retries - using Hangfire built-in increasing exponential backoff
/// (no concrete delay list configured; SRS specifies no delay). When retry
/// attempts are exhausted the job stays in the Failed state (never deleted).
/// Note: state-election tests prove the retry policy semantics only; they do
/// not prove PostgreSQL persistence of failed jobs.
/// </summary>
public sealed class WelcomeEmailRetryConfigurationTests
{
    [Fact]
    public void RetryPolicy_ShouldAllowFiveTotalExecutions_WithBuiltInBackoff_AndFailAtEnd()
    {
        // Arrange
        var attribute = typeof(WelcomeEmailJob)
            .GetMethod(nameof(WelcomeEmailJob.SendWelcomeEmailAsync))!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: false)
            .Cast<AutomaticRetryAttribute>()
            .Single();

        // Assert: 1 initial run + 4 retries = 5 executions. DelaysInSeconds is
        // null because no custom delay list is configured.
        attribute.Attempts.Should().Be(WelcomeEmailJob.RetryAttempts);
        attribute.Attempts.Should().Be(4);
        attribute.DelaysInSeconds.Should().BeNull("no custom delay list; Hangfire uses its built-in exponential backoff");
        attribute.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
        attribute.LogEvents.Should().BeTrue();
    }

    // State-election semantics: Hangfire counts retries via the "RetryCount"
    // job parameter (read through ElectStateContext.GetRetryCount()).
    // RetryCount 0..3 (below Attempts=4) must reschedule the failed job to
    // ScheduledState with a positive delay (built-in exponential backoff).
    // RetryCount=4 (attempts exhausted) must keep FailedState - never
    // DeletedState - so the job is retained for admin inspection.
    // NOTE: these tests prove the retry policy only; they do not prove
    // PostgreSQL persistence.
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    public void OnStateElection_WhenRetriesRemain_ShouldRescheduleFailedJob_ToScheduledState(string recordedRetryCount)
    {
        // Arrange
        var attribute = GetRetryAttribute();
        var connection = new Mock<IStorageConnection>();
        connection
            .Setup(c => c.GetJobParameter(It.IsAny<string>(), "RetryCount"))
            .Returns(recordedRetryCount);

        var context = CreateElectionContext(connection.Object);
        context.CandidateState = new FailedState(new InvalidOperationException("SMTP connection failed"));

        // Act
        attribute.OnStateElection(context);

        // Assert
        var scheduled = context.CandidateState.Should().BeOfType<ScheduledState>().Subject;
        scheduled.EnqueueAt.Should().BeAfter(
            DateTime.UtcNow,
            "Hangfire built-in exponential backoff must schedule the retry in the future (positive delay)");
    }

    [Fact]
    public void OnStateElection_WhenAllRetriesExhausted_ShouldKeepFailedState_AndNeverDelete()
    {
        // Arrange: RetryCount=4 means the initial run + 4 retries (5 executions
        // total) have all failed; attempts are exhausted.
        var attribute = GetRetryAttribute();
        var connection = new Mock<IStorageConnection>();
        connection
            .Setup(c => c.GetJobParameter(It.IsAny<string>(), "RetryCount"))
            .Returns("4");

        var context = CreateElectionContext(connection.Object);
        context.CandidateState = new FailedState(new InvalidOperationException("SMTP connection failed"));

        // Act
        attribute.OnStateElection(context);

        // Assert: OnAttemptsExceeded=Fail keeps the candidate in FailedState;
        // DeletedState is never elected, so the failed job is retained.
        context.CandidateState.Should().BeOfType<FailedState>(
            "OnAttemptsExceeded=Fail must keep the failed job, never delete it");
    }

    private static AutomaticRetryAttribute GetRetryAttribute() =>
        typeof(WelcomeEmailJob)
            .GetMethod(nameof(WelcomeEmailJob.SendWelcomeEmailAsync))!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: false)
            .Cast<AutomaticRetryAttribute>()
            .Single();

    private static ElectStateContext CreateElectionContext(IStorageConnection connection)
    {
        var job = new Job(
            typeof(WelcomeEmailJob).GetMethod(nameof(WelcomeEmailJob.SendWelcomeEmailAsync))!,
            new object[] { "user-123", "newuser@example.com", "New Chef" });
        var backgroundJob = new BackgroundJob("job-42", job, DateTime.UtcNow);

        var applyContext = new ApplyStateContext(
            Mock.Of<JobStorage>(),
            connection,
            Mock.Of<IWriteOnlyTransaction>(),
            backgroundJob,
            new EnqueuedState(),
            EnqueuedState.StateName);

        return new ElectStateContext(applyContext);
    }
}
