namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// Marker interface for a single-dependency health probe. Implementations live
/// in the Infrastructure layer; the API layer wraps them in a health check so
/// that the check itself stays dependency-agnostic.
///
/// A probe returns <c>true</c> only when the dependency is usable. It must not
/// throw for an unreachable dependency, but a throwing probe is still handled
/// by the consuming health check.
/// </summary>
public interface IDependencyProbe
{
    Task<bool> ProbeAsync(CancellationToken ct);
}