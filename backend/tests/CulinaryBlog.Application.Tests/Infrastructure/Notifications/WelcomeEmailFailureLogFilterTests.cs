using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

/// <summary>
/// Verifies the FR-JOB-001 failure filter: an explicit Error (with job id,
/// userId and email context, and the original exception) is logged only when a
/// WelcomeEmailJob enters the Failed state; other states or other job types are
/// ignored. Never logs secrets.
/// </summary>
public sealed class WelcomeEmailFailureLogFilterTests
{
    private readonly CapturingLogger _logger = new();
    private readonly WelcomeEmailFailureLogFilter _filter;

    public WelcomeEmailFailureLogFilterTests()
    {
        _filter = new WelcomeEmailFailureLogFilter(_logger);
    }

    [Fact]
    public void OnStateApplied_WhenWelcomeJobEntersFailedState_ShouldLogErrorWithJobContext()
    {
        // Arrange
        var exception = new InvalidOperationException("SMTP connection failed");
        var context = CreateContext(
            new FailedState(exception),
            CreateWelcomeJob("user-123", "newuser@example.com"));

        // Act
        _filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert: exactly one Error log carrying the original exception and the
        // operational context (job id, userId, email) for triage.
        var entry = _logger.Entries.Should().ContainSingle().Which;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.Message.Should()
            .Contain("job-42")
            .And.Contain("user-123")
            .And.Contain("newuser@example.com");
    }

    [Fact]
    public void OnStateApplied_WhenStateIsNotFailed_ShouldNotLog()
    {
        // Arrange: intermediate states (e.g. Enqueued) must stay silent.
        var context = CreateContext(
            new EnqueuedState(),
            CreateWelcomeJob("user-123", "newuser@example.com"));

        // Act
        _filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void OnStateApplied_WhenFailedJobIsNotWelcomeEmailJob_ShouldNotLog()
    {
        // Arrange: the filter is scoped to WelcomeEmailJob only.
        var otherJob = new Job(typeof(NotAWelcomeEmailJob).GetMethod(nameof(NotAWelcomeEmailJob.Run))!);
        var context = CreateContext(
            new FailedState(new InvalidOperationException("boom")),
            otherJob);

        // Act
        _filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void OnStateUnapplied_ShouldNotLog()
    {
        // Arrange
        var context = CreateContext(
            new FailedState(new InvalidOperationException("boom")),
            CreateWelcomeJob("user-123", "newuser@example.com"));

        // Act
        _filter.OnStateUnapplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert
        _logger.Entries.Should().BeEmpty();
    }

    private static ApplyStateContext CreateContext(IState newState, Job job)
    {
        var backgroundJob = new BackgroundJob("job-42", job, DateTime.UtcNow);

        return new ApplyStateContext(
            Mock.Of<JobStorage>(),
            Mock.Of<IStorageConnection>(),
            Mock.Of<IWriteOnlyTransaction>(),
            backgroundJob,
            newState,
            EnqueuedState.StateName);
    }

    private static Job CreateWelcomeJob(string userId, string email)
    {
        var method = typeof(WelcomeEmailJob)
            .GetMethod(nameof(WelcomeEmailJob.SendWelcomeEmailAsync))!;

        return new Job(method, new object[] { userId, email, "New Chef" });
    }

    private sealed class NotAWelcomeEmailJob
    {
        public void Run()
        {
        }
    }

    private sealed class CapturingLogger : ILogger<WelcomeEmailJob>
    {
        public List<CapturedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedLog(logLevel, exception, formatter(state, exception)));
        }
    }

    private sealed record CapturedLog(LogLevel Level, Exception? Exception, string Message);
}
