using CulinaryBlog.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Health;

/// <summary>
/// FR-OBS-001: when Redis is not configured, no
/// <see cref="IConnectionMultiplexer"/> exists in the container (the container
/// registers the distributed memory cache instead), and the probe must report
/// unhealthy without throwing - that is what turns <c>/health/ready</c> into a
/// 503 on a deployment that forgot Redis.
///
/// The container here is deliberately empty: the probe's own contract is that a
/// missing service means "unhealthy", not "skip". No live Redis is required and
/// nothing is ever connected to.
/// </summary>
public sealed class RedisProbeTests
{
    [Fact]
    public async Task ProbeAsync_RedisNotConfigured_ReturnsFalse()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var probe = new RedisProbe(services);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_RedisNotConfigured_DoesNotThrow()
    {
        // Stated separately from the value assertion because the failure mode
        // that matters is an exception escaping into the health pipeline: it
        // would surface as an unhandled error instead of Unhealthy.
        using var services = new ServiceCollection().BuildServiceProvider();
        var probe = new RedisProbe(services);

        var act = async () => await probe.ProbeAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}