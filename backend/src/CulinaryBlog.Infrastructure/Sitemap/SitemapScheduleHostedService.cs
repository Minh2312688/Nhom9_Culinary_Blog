using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Hosting;

namespace CulinaryBlog.Infrastructure.Sitemap;

public sealed class SitemapScheduleHostedService : IHostedService
{
    public const string RecurringJobId = "daily-sitemap-generation";
    public const string CronExpression = "0 2 * * *";

    private readonly IRecurringJobManager recurringJobManager;
    private bool isRegistered;

    public SitemapScheduleHostedService(IRecurringJobManager recurringJobManager)
    {
        this.recurringJobManager = recurringJobManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (isRegistered)
        {
            return Task.CompletedTask;
        }

        recurringJobManager.AddOrUpdate(
            RecurringJobId,
            Job.FromExpression<SitemapJob>(job =>
                job.GenerateAndPublishAsync(CancellationToken.None)),
            CronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        isRegistered = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
