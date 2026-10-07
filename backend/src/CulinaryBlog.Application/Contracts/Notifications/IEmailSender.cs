using System.Threading;

namespace CulinaryBlog.Application.Contracts.Notifications;

/// <summary>
/// Sends an email message. Implemented by Infrastructure (MailKit/SMTP).
/// FR-JOB-001: abstraction allows the Hangfire worker to be unit tested
/// without a real SMTP server.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null);

public interface IEmailSender
{
    /// <summary>
    /// Sends the message. Throws on failure so the caller (Hangfire worker)
    /// can surface retry/failure states. Never returns fake success.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
