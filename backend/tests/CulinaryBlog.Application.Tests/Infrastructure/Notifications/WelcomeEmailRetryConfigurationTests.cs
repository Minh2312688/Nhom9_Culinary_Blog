using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Hangfire;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

/// <summary>
/// Verifies the FR-JOB-001 retry contract: 3 retries after the first run with
/// delays of 1 minute, 5 minutes and 30 minutes, ending in Failed state.
/// </summary>
public sealed class WelcomeEmailRetryConfigurationTests
{
    [Fact]
    public void RetryPolicy_ShouldDeclareThreeRetries_WithExpectedDelays_AndFailAtEnd()
    {
        // Arrange
        var attribute = typeof(WelcomeEmailJob)
            .GetMethod(nameof(WelcomeEmailJob.SendWelcomeEmailAsync))!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: false)
            .Cast<AutomaticRetryAttribute>()
            .Single();

        // Assert: 3 retries after the initial run...
        attribute.Attempts.Should().Be(WelcomeEmailJob.RetryAttempts);
        attribute.Attempts.Should().Be(3);

        // ...with delays of 1m, 5m, 30m...
        var delays = WelcomeEmailJob.RetryDelaysInSeconds;
        delays.Should().Equal(60, 300, 1800);
        attribute.DelaysInSeconds.Should().Equal(60, 300, 1800);

        // ...and job goes to Failed when retries are exhausted.
        attribute.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }
}
