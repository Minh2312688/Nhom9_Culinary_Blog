using System.Diagnostics.Metrics;

namespace CulinaryBlog.Application.Common.Observability;

/// <summary>
/// FR-OBS-003 recipe metrics. Owns the <c>CulinaryBlog</c> meter and exposes
/// counters for recipes created/published plus a histogram for operation
/// duration. TV2 handlers call these methods (O-09..O-11); the meter is
/// registered here so handlers do not need to reference Infrastructure.
/// </summary>
public sealed class RecipeMetrics
{
    public const string Create = "create";
    public const string Publish = "publish";
    public const string Success = "success";
    public const string Failure = "failure";

    private static readonly Meter Meter = new("CulinaryBlog");

    private static readonly Counter<long> CreatedCounter =
        Meter.CreateCounter<long>("culinaryblog.recipes.created", unit: "recipes");

    private static readonly Counter<long> PublishedCounter =
        Meter.CreateCounter<long>("culinaryblog.recipes.published", unit: "recipes");

    private static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("culinaryblog.recipe.operation.duration", unit: "s");

    public void RecordCreated()
    {
        CreatedCounter.Add(1);
    }

    public void RecordPublished()
    {
        PublishedCounter.Add(1);
    }

    public void RecordDuration(string operation, string outcome, double seconds)
    {
        if (operation is not Create and not Publish)
        {
            throw new ArgumentException($"Invalid operation: {operation}", nameof(operation));
        }

        if (outcome is not Success and not Failure)
        {
            throw new ArgumentException($"Invalid outcome: {outcome}", nameof(outcome));
        }

        OperationDuration.Record(seconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", outcome));
    }
}