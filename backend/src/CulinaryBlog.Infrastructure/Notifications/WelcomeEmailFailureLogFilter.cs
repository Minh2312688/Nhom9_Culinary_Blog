using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// Logs an explicit Error when the FR-JOB-001 welcome email job exhausts its
/// retries and enters the Failed state. Registered as an instance (with
/// injected <see cref="ILogger{TCategoryName}"/>) in Hangfire global filters;
/// only acts on <see cref="WelcomeEmailJob"/> jobs. Never logs secrets.
/// </summary>
public sealed class WelcomeEmailFailureLogFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly ILogger<WelcomeEmailJob> _logger;

    public WelcomeEmailFailureLogFilter(ILogger<WelcomeEmailJob> logger)
    {
        _logger = logger;
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState)
        {
            return;
        }

        if (context.BackgroundJob.Job.Type != typeof(WelcomeEmailJob))
        {
            return;
        }

        _logger.LogError(
            failedState.Exception,
            "FR-JOB-001 welcome email job {JobId} failed permanently after all retries. UserId: {UserId}, Email: {Email}.",
            context.BackgroundJob.Id,
            JobArgument(context.BackgroundJob.Job, 0),
            JobArgument(context.BackgroundJob.Job, 1));
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    private static string JobArgument(Job job, int index) =>
        job.Args.Count > index ? job.Args[index]?.ToString() ?? "(null)" : "(null)";
}
