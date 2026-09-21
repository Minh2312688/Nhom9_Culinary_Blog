using CulinaryBlog.Application.Contracts.Authentication;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// Minimal welcome email enqueuer placeholder.
/// FR-JOB-001 (Welcome Email via Hangfire) belongs to TV4.
/// </summary>
public class WelcomeEmailEnqueuer : IWelcomeEmailEnqueuer
{
    private readonly ILogger<WelcomeEmailEnqueuer> _logger;

    public WelcomeEmailEnqueuer(ILogger<WelcomeEmailEnqueuer> logger)
    {
        _logger = logger;
    }

    public Task EnqueueWelcomeEmailAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[TV4 Dependency] EnqueueWelcomeEmail requested for {Email} ({DisplayName}). Waiting for TV4 FR-JOB-001 Hangfire infrastructure.",
            email,
            displayName);

        return Task.CompletedTask;
    }
}
