using CulinaryBlog.Application.Contracts.Notifications;
using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

public sealed class WelcomeEmailJobTests
{
    private readonly Mock<IEmailSender> _senderMock = new();
    private readonly WelcomeEmailJob _job;

    public WelcomeEmailJobTests()
    {
        _job = new WelcomeEmailJob(
            _senderMock.Object,
            NullLogger<WelcomeEmailJob>.Instance);
    }

    [Fact]
    public async Task Send_ShouldDeliverToRecipient_WithDisplayName()
    {
        // Arrange
        const string userId = "user-123";
        const string email = "newuser@example.com";
        const string displayName = "New Chef";

        // Act
        await _job.SendWelcomeEmailAsync(userId, email, displayName);

        // Assert
        _senderMock.Verify(x => x.SendAsync(
            It.Is<EmailMessage>(m =>
                m.To == email &&
                m.HtmlBody.Contains("New Chef") &&
                !string.IsNullOrWhiteSpace(m.Subject)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_ShouldHtmlEncodeDisplayName()
    {
        // Arrange: hostile display name must not break HTML.
        const string hostile = "<script>alert('x')</script>";

        // Act
        await _job.SendWelcomeEmailAsync("u1", "a@b.c", hostile);

        // Assert
        _senderMock.Verify(x => x.SendAsync(
            It.Is<EmailMessage>(m =>
                !m.HtmlBody.Contains("<script>") &&
                m.HtmlBody.Contains("&lt;script&gt;")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_WhenSmtpFails_ShouldThrow_NotReportSuccess()
    {
        // Arrange: SMTP failure must propagate so Hangfire retries.
        _senderMock
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        // Act & Assert: exception propagates, no fake success.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _job.SendWelcomeEmailAsync("u1", "a@b.c", "Chef"));

        _senderMock.Verify(x => x.SendAsync(
            It.IsAny<EmailMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "a@b.c", "Chef")]
    [InlineData("", "a@b.c", "Chef")]
    [InlineData("u1", null, "Chef")]
    [InlineData("u1", "a@b.c", null)]
    public async Task Send_WithMissingPayload_ShouldThrow(string? userId, string? email, string? displayName)
    {
        // Act & Assert: any ArgumentException derivative (ArgumentNullException
        // included) is correct for missing payload; nothing may be sent.
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _job.SendWelcomeEmailAsync(userId!, email!, displayName!));

        _senderMock.Verify(x => x.SendAsync(
            It.IsAny<EmailMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
