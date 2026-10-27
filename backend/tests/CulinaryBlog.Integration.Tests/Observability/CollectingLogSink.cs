using Serilog.Core;
using Serilog.Events;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// Thread-safe ILogEventSink that stores events in a list so tests can inspect
/// what the Serilog pipeline produced. Tests register it in DI; the app's
/// ReadFrom.Services call in SerilogSetup picks it up.
/// </summary>
public sealed class CollectingLogSink : ILogEventSink
{
    private readonly List<LogEvent> events = new();
    private readonly object gate = new();

    public void Emit(LogEvent logEvent)
    {
        lock (gate)
        {
            events.Add(logEvent);
        }
    }

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (gate)
            {
                return events.ToList();
            }
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            events.Clear();
        }
    }
}
