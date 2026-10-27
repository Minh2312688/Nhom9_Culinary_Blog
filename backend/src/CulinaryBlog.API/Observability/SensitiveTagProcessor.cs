using System.Diagnostics;
using OpenTelemetry;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// FR-OBS-003 / O-03: removes tags that may carry query strings, full URLs or
/// SQL statement text from completed activities before they reach any exporter.
/// A null value passed to <see cref="Activity.SetTag(string, object?)"/> removes
/// the tag entirely.
/// </summary>
public sealed class SensitiveTagProcessor : BaseProcessor<Activity>
{
    private static readonly string[] SensitiveTags =
    [
        "url.query",
        "url.full",
        "http.url",
        "http.target",
        "db.statement",
        "db.query.text"
    ];

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in SensitiveTags)
        {
            activity.SetTag(tag, null);
        }

        base.OnEnd(activity);
    }
}
