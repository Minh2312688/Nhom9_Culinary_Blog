using CulinaryBlog.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Health;

/// <summary>
/// FR-OBS-001: MinIO is reported by the aggregate <c>/health</c> endpoint but
/// does not affect readiness. When MinIO is not configured the container
/// registers <c>UnavailableFileStorageService</c> and no <c>IMinioClient</c>, so
/// the probe must report unhealthy without throwing and without attempting a
/// network call.
///
/// The container is empty on purpose (see <see cref="RedisProbeTests"/>); the
/// configuration is empty too, so the bucket name falls back to the default and
/// is never used, because the missing client short-circuits first.
/// </summary>
public sealed class MinioProbeTests
{
    [Fact]
    public async Task ProbeAsync_MinioNotConfigured_ReturnsFalse()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new ConfigurationBuilder().Build();
        var probe = new MinioProbe(services, configuration);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_MinioNotConfigured_DoesNotThrow()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var configuration = new ConfigurationBuilder().Build();
        var probe = new MinioProbe(services, configuration);

        var act = async () => await probe.ProbeAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}