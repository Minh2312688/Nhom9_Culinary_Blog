using System.Net;
using CulinaryBlog.Application.Contracts.Notifications;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// FR-JOB-001 worker: renders the welcome email HTML and sends it via
/// <see cref="IEmailSender"/> (MailKit/SMTP).
/// Retry policy (SRS-AUDIT §12.4, group-confirmed current policy): at most
/// 5 executions in total - the initial run plus at most 4 retries - using
/// Hangfire's built-in increasing exponential backoff between attempts
/// (the SRS specifies no concrete delay). When retries are exhausted
/// the job stays in the Failed state (OnAttemptsExceeded=Fail, never
/// deleted). The group accepts persistent Failed jobs + Dashboard
/// Failed tab + failure Error log as the DLQ-equivalent inspection
/// for this FR. SRS-AUDIT §3.6 (3 retries / 1/5/30 minutes) is
/// the outdated conflicting text and must not be used for config.
/// stays in the Failed state (no catch-and-report-success, no nested retry).
/// Keep the signature to serializable primitives only: userId, email, displayName.
/// </summary>
public sealed class WelcomeEmailJob
{
    public const int RetryAttempts = 4;
    public static readonly int[] RetryDelaysInSeconds = [];

    private readonly IEmailSender _emailSender;
    private readonly ILogger<WelcomeEmailJob> _logger;

    public WelcomeEmailJob(IEmailSender emailSender, ILogger<WelcomeEmailJob> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = RetryAttempts, OnAttemptsExceeded = AttemptsExceededAction.Fail, LogEvents = true)]
    public async Task SendWelcomeEmailAsync(string userId, string email, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var safeDisplayName = WebUtility.HtmlEncode(displayName);

        var message = new EmailMessage(
            To: email,
            Subject: "Welcome to Culinary Blog!",
            HtmlBody: $"<h1>Welcome, {safeDisplayName}!</h1><p>Thanks for joining Culinary Blog. Happy cooking!</p>",
            TextBody: $"Welcome, {displayName}! Thanks for joining Culinary Blog. Happy cooking!");

        // Let SMTP failures propagate: Hangfire sees the exception and applies
        // the retry policy above. Never swallow into fake success.
        await _emailSender.SendAsync(message);

        _logger.LogInformation("Welcome email sent to user {UserId} at {Email}.", userId, email);
    }
}
