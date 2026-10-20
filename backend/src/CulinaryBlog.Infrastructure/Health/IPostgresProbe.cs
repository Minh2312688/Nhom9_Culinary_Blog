namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// Probes PostgreSQL. PostgreSQL is required for readiness (FR-OBS-001).
/// </summary>
public interface IPostgresProbe : IDependencyProbe;