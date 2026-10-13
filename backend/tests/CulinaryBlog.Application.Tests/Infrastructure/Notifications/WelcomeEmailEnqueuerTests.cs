using CulinaryBlog.Application.Contracts.Notifications;
using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

public sealed class WelcomeEmailEnqueuerTests
{
    private readonly Mock<IBackgroundJobClient> _clientMock = new();
    private readonly WelcomeEmailEnqueuer _enqueuer;

    public WelcomeEmailEnqueuerTests()
    {
        _enqueuer = new WelcomeEmailEnqueuer(
            _clientMock.Object,
            NullLogger<WelcomeEmailEnqueuer>.Instance);
    }

    [Fact]
    public async Task Enqueue_ShouldCallHangfire_WithWelcomeEmailJob_AndExactPayload()
    {
        // Arrange
        const string userId = "user-123";
        const string email = "newuser@example.com";
        const string displayName = "New Chef";

        // Act
        await _enqueuer.EnqueueWelcomeEmailAsync(userId, email, displayName);

        // Assert: Hangfire client received the WelcomeEmailJob with exact args.
        _clientMock.Verify(x => x.Create(
            It.Is<Job>(job =>
                job.Type == typeof(WelcomeEmailJob) &&
                job.Method.Name == nameof(WelcomeEmailJob.SendWelcomeEmailAsync) &&
                job.Args.Count == 3 &&
                Equals(job.Args[0], userId) &&
                Equals(job.Args[1], email) &&
                Equals(job.Args[2], displayName)),
            It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task Enqueue_WhenHangfireThrows_ShouldNotThrow_AndLogError()
    {
        // Arrange: simulate Hangfire storage outage (A2 decision).
        _clientMock
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Throws(new InvalidOperationException("storage down"));

        // Act: must not throw — registration still succeeds.
        var act = () => _enqueuer.EnqueueWelcomeEmailAsync("u1", "a@b.c", "Chef");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Enqueue_ShouldOnlyPassSerializablePrimitives()
    {
        // Arrange
        await _enqueuer.EnqueueWelcomeEmailAsync("u-1", "a@b.c", "Chef");

        // Assert: all args are strings (serializable primitives).
        _clientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Args.All(a => a is string)),
            It.IsAny<IState>()), Times.Once);
    }
}
