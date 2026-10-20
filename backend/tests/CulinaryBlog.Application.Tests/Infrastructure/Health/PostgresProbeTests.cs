using CulinaryBlog.Infrastructure.Health;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Health;

/// <summary>
/// FR-OBS-001: the PostgreSQL readiness probe must answer with a boolean and
/// must never throw, because a thrown exception would surface as an unhandled
/// failure instead of the intended Unhealthy status.
///
/// These tests build their own <see cref="ApplicationDbContext"/> options and
/// never touch a live database: the reachable case uses the in-memory
/// provider, the unreachable case points Npgsql at a closed local port (which
/// refuses the connection immediately, so <c>Timeout=1</c> is never reached).
/// </summary>
public sealed class PostgresProbeTests
{
    [Fact]
    public async Task ProbeAsync_ReachableDatabase_ReturnsTrue()
    {
        await using var db = CreateContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"postgres-probe-{Guid.NewGuid()}")
                .Options);
        var probe = new PostgresProbe(db);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeAsync_UnreachableDatabase_ReturnsFalseWithoutThrowing()
    {
        // Port 1 on the loopback interface has no listener, so the connection
        // is refused rather than left to time out. The credentials are dummy
        // values and are never sent anywhere.
        const string unreachable =
            "Host=127.0.0.1;Port=1;Database=unreachable;Username=dummy;Password=dummy;Timeout=1";

        await using var db = CreateContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(unreachable)
                .Options);
        var probe = new PostgresProbe(db);

        // The assertion is the absence of an exception as much as the value:
        // CanConnectAsync swallows the connection error by design.
        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    private static ApplicationDbContext CreateContext(DbContextOptions<ApplicationDbContext> options) =>
        new(options);
}