using System.Diagnostics;
using CulinaryBlog.API.Observability;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// OT-09: verifies O-03's SensitiveTagProcessor removes all six sensitive tags
/// (url.query, url.full, http.url, http.target, db.statement, db.query.text)
/// while keeping a non-sensitive tag (http.request.method).
/// </summary>
public sealed class SensitiveTagProcessorTests
{
    private static readonly string[] SensitiveTags =
    [
        "url.query",
        "url.full",
        "http.url",
        "http.target",
        "db.statement",
        "db.query.text",
    ];

    [Fact]
    public void OnEnd_RemovesAllSixSensitiveTagsAndKeepsOtherTags()
    {
        var activity = new Activity("test");
        activity.SetTag("url.query", "SENTINEL-QUERY-123");
        activity.SetTag("url.full", "SENTINEL-FULL-URL-123");
        activity.SetTag("http.url", "SENTINEL-HTTP-URL-123");
        activity.SetTag("http.target", "SENTINEL-HTTP-TARGET-123");
        activity.SetTag("db.statement", "SENTINEL-DB-STATEMENT-123");
        activity.SetTag("db.query.text", "SENTINEL-DB-QUERY-TEXT-123");
        activity.SetTag("http.request.method", "GET");

        new SensitiveTagProcessor().OnEnd(activity);

        foreach (var tag in SensitiveTags)
        {
            Assert.Null(activity.GetTagItem(tag));
        }

        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
    }
}
