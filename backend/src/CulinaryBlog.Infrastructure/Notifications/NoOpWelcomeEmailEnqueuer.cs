using CulinaryBlog.Application.Contracts.Authentication;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// Testing-environment fallback for <see cref="IWelcomeEmailEnqueuer"/>.
/// Used only when Hangfire is not registered (no <c>IBackgroundJobClient</c>),
/// so the DI graph still resolves. Logs a warning — never claims the job was
/// enqueued. Integration tests override the seam with a spy when they need to
/// assert enqueue behavior.
/// </summary>
internal sealed class NoOpWelcomeEmailEnqueuer : IWelcomeEmailEnqueuer
{
    private readonly ILogger<NoOpWelcomeEmailEnqueuer> _logger;

    public NoOpWelcomeEmailEnqueuer(ILogger<NoOpWelcomeEmailEnqueuer> logger)
    {
        _logger = logger;
    }

    public Task EnqueueWelcomeEmailAsync(
        string userId,
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "FR-JOB-001 welcome email for user {UserId} at {Email} was NOT enqueued: Hangfire is not registered in this environment.",
            userId,
            email);
        return Task.CompletedTask;
    }
}
