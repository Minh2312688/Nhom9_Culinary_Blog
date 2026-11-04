using System.Diagnostics.Metrics;
using CulinaryBlog.Application.Common.Observability;
using Xunit;

namespace CulinaryBlog.Application.Tests.Observability;

[Collection("Meters")]
public class RecipeMetricsTests
{
    [Fact]
    public void RecordCreated_IncrementsCounter()
    {
        var metrics = new RecipeMetrics();
        using var listener = new MeterListener();
        var createdMeasurements = new List<long>();
        string? createdUnit = null;
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "CulinaryBlog" && instrument.Name == "culinaryblog.recipes.created")
            {
                createdUnit = instrument.Unit;
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            if (instrument.Name == "culinaryblog.recipes.created")
            {
                createdMeasurements.Add(value);
            }
        });
        listener.Start();
        metrics.RecordCreated();
        metrics.RecordCreated();
        Assert.Equal("recipes", createdUnit);
        Assert.Equal(new long[] { 1, 1 }, createdMeasurements);
    }

    [Fact]
    public void RecordPublished_IncrementsCounter()
    {
        var metrics = new RecipeMetrics();
        using var listener = new MeterListener();
        var publishedMeasurements = new List<long>();
        string? publishedUnit = null;
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "CulinaryBlog" && instrument.Name == "culinaryblog.recipes.published")
            {
                publishedUnit = instrument.Unit;
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            if (instrument.Name == "culinaryblog.recipes.published")
            {
                publishedMeasurements.Add(value);
            }
        });
        listener.Start();
        metrics.RecordPublished();
        metrics.RecordPublished();
        Assert.Equal("recipes", publishedUnit);
        Assert.Equal(new long[] { 1, 1 }, publishedMeasurements);
    }

    [Fact]
    public void RecordDuration_RecordsWithTags()
    {
        var metrics = new RecipeMetrics();
        using var listener = new MeterListener();
        double durationValue = 0;
        var tagsList = new List<KeyValuePair<string, object?>>();
        string? durationUnit = null;
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "CulinaryBlog" && instrument.Name == "culinaryblog.recipe.operation.duration")
            {
                durationUnit = instrument.Unit;
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            if (instrument.Name == "culinaryblog.recipe.operation.duration")
            {
                durationValue = value;
                tagsList.Clear();
                foreach (var tag in tags)
                {
                    tagsList.Add(tag);
                }
            }
        });
        listener.Start();
        metrics.RecordDuration(RecipeMetrics.Create, RecipeMetrics.Success, 0.5);
        Assert.Equal("s", durationUnit);
        Assert.Equal(0.5, durationValue);
        Assert.Equal(new[] { "operation", "outcome" }, tagsList.Select(tag => tag.Key).OrderBy(key => key));
        Assert.Equal(RecipeMetrics.Create, tagsList.Single(tag => tag.Key == "operation").Value);
        Assert.Equal(RecipeMetrics.Success, tagsList.Single(tag => tag.Key == "outcome").Value);
    }

    [Fact]
    public void RecordDuration_InvalidOperation_Throws()
    {
        var metrics = new RecipeMetrics();
        Assert.Throws<ArgumentException>(() => metrics.RecordDuration("invalid", RecipeMetrics.Success, 1.0));
    }

    [Fact]
    public void RecordDuration_InvalidOutcome_Throws()
    {
        var metrics = new RecipeMetrics();
        Assert.Throws<ArgumentException>(() => metrics.RecordDuration(RecipeMetrics.Create, "invalid", 1.0));
    }
}

[CollectionDefinition("Meters", DisableParallelization = true)]
public class MetersCollection : ICollectionFixture<object>
{
}
