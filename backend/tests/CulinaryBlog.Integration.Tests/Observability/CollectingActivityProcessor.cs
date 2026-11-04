using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// Shared activity processor for OpenTelemetry integration tests. Collects
/// activities in a thread-safe list so tests can inspect server/client spans
/// and verify tag/content requirements (OT-01, OT-02, OT-10, OT-05).
/// </summary>
public sealed class CollectingActivityProcessor : BaseProcessor<Activity>
{
    private readonly List<Activity> _activities;

    public IReadOnlyList<Activity> Activities => _activities;

    public CollectingActivityProcessor() : this(new List<Activity>()) { }

    public CollectingActivityProcessor(List<Activity> activities)
    {
        _activities = activities;
    }

    public override void OnEnd(Activity activity)
    {
        lock (_activities)
        {
            _activities.Add(activity);
        }
        base.OnEnd(activity);
    }
}