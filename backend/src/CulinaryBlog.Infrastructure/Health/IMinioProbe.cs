namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// Probes MinIO object storage. MinIO is reported by the aggregate health
/// endpoint but does NOT affect readiness (FR-OBS-001).
/// </summary>
public interface IMinioProbe : IDependencyProbe;