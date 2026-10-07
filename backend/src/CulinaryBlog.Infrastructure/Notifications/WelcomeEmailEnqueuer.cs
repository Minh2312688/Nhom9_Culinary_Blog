using CulinaryBlog.Application.Contracts.Authentication;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// FR-JOB-001: enqueues the welcome email as a Hangfire background job.
/// Only serializable primitives (userId, email, displayName) cross the queue
/// boundary — never entities, services or streams.
/// Per approved decision A2: enqueue failures after user creation must not fail
/// registration. Catch, log Error with context (no secrets), and let the API
/// still return success. Never report the job as enqueued when it was not.
/// </summary>
public sealed class WelcomeEmailEnqueuer : IWelcomeEmailEnqueuer
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<WelcomeEmailEnqueuer> _logger;

    public WelcomeEmailEnqueuer(
        IBackgroundJobClient backgroundJobClient,
        ILogger<WelcomeEmailEnqueuer> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public Task EnqueueWelcomeEmailAsync(
        string userId,
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _backgroundJobClient.Enqueue<WelcomeEmailJob>(
                job => job.SendWelcomeEmailAsync(userId, email, displayName));
        }
        catch (Exception ex)
        {
            // A2: user already exists — do not fail registration because the
            // email could not be queued. Explicit error log, no success claim.
            _logger.LogError(ex,
                "FR-JOB-001 failed to enqueue welcome email for user {UserId} at {Email}. Registration succeeded; email delivery will not be retried automatically.",
                userId,
                email);
        }

        return Task.CompletedTask;
    }
}
