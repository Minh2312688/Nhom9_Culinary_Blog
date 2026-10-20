namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// Probes Redis. Redis is required for readiness (FR-OBS-001).
/// </summary>
public interface IRedisProbe : IDependencyProbe;