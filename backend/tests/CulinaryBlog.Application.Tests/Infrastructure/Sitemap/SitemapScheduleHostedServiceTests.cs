using CulinaryBlog.Infrastructure.Sitemap;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Sitemap;

public sealed class SitemapScheduleHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RegistersDailyScheduleInUtcOnlyOnce()
    {
        Job? registeredJob = null;
        string? registeredId = null;
        string? registeredCron = null;
        RecurringJobOptions? registeredOptions = null;
        var manager = new Mock<IRecurringJobManager>();
        manager.Setup(service => service.AddOrUpdate(
                It.IsAny<string>(),
                It.IsAny<Job>(),
                It.IsAny<string>(),
                It.IsAny<RecurringJobOptions>()))
            .Callback<string, Job, string, RecurringJobOptions>((id, job, cron, options) =>
            {
                registeredId = id;
                registeredJob = job;
                registeredCron = cron;
                registeredOptions = options;
            });
        var service = new SitemapScheduleHostedService(manager.Object);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        manager.Verify(provider => provider.AddOrUpdate(
            It.IsAny<string>(), It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()),
            Times.Once);
        registeredId.Should().Be("daily-sitemap-generation");
        registeredCron.Should().Be("0 2 * * *");
        registeredOptions!.TimeZone.Should().Be(TimeZoneInfo.Utc);
        registeredJob!.Type.Should().Be(typeof(SitemapJob));
        registeredJob.Method.Name.Should().Be(nameof(SitemapJob.GenerateAndPublishAsync));
    }
}
