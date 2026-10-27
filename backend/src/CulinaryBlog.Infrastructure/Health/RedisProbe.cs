using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// Redis readiness probe. Redis is required for readiness (FR-OBS-001), so a
/// missing multiplexer is reported as unhealthy rather than skipped: when
/// <c>Redis:ConnectionString</c> is unset the DI container registers the
/// distributed memory cache and no <see cref="IConnectionMultiplexer"/>, and
/// the memory cache is never Redis.
///
/// The probe reuses the singleton multiplexer owned by DI - it never opens its
/// own connection and never writes a key, so probing is side-effect free.
/// </summary>
public sealed class RedisProbe : IRedisProbe
{
    private readonly IServiceProvider services;

    public RedisProbe(IServiceProvider services)
    {
        this.services = services;
    }

    public async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var multiplexer = services.GetService<IConnectionMultiplexer>();
        if (multiplexer is null)
        {
            return false;
        }

        // PING is the cheapest round trip that proves the connection is usable.
        // The multiplexer's own command timeout bounds the work; WaitAsync makes
        // the probe abandon it at the health check timeout even if it does not.
        await multiplexer.GetDatabase().PingAsync().WaitAsync(ct);

        return true;
    }
}