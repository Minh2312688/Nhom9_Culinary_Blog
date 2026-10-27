using CulinaryBlog.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// Adapts an <see cref="IDependencyProbe"/> to <see cref="IHealthCheck"/>.
/// Only the API project has <c>IHealthCheck</c> available, so the probes stay
/// in Infrastructure and the health-check plumbing stays here.
///
/// The probe is awaited through <see cref="Task.WaitAsync(CancellationToken)"/>
/// so a probe that ignores its cancellation token is still abandoned when the
/// health-check timeout elapses. Any failure - a <c>false</c> result, a thrown
/// exception or a timeout - is reported as Unhealthy with no description, so
/// no dependency detail (host name, connection string, exception text) can
/// reach a response. This method never rethrows.
/// </summary>
public sealed class ProbeHealthCheck<TProbe> : IHealthCheck
    where TProbe : IDependencyProbe
{
    private readonly TProbe probe;

    public ProbeHealthCheck(TProbe probe)
    {
        this.probe = probe;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await probe.ProbeAsync(cancellationToken).WaitAsync(cancellationToken);
            return isHealthy
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception)
        {
            // Includes OperationCanceledException from the timeout above and
            // from a caller-side cancellation. Deliberately swallowed: the
            // health check reports status only, never a reason.
            return HealthCheckResult.Unhealthy();
        }
    }
}