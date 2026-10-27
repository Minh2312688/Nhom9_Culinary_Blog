using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Exceptions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Domain.Entities;

public sealed class RecipePublishTests
{
    [Fact]
    public void Publish_WithoutIngredientsOrSteps_ThrowsDomainRuleViolation()
    {
        var recipe = new Recipe();

        Assert.Throws<DomainRuleViolationException>(() => recipe.Publish(DateTimeOffset.UtcNow));
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
        Assert.Null(recipe.PublishedAt);
    }

    [Fact]
    public void Publish_SetsTheLatestPublicationTimestamp()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
        var recipe = new Recipe();
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Flour" });
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });

        recipe.Publish(publishedAt);

        Assert.Equal(publishedAt, recipe.PublishedAt);
        Assert.Equal(RecipeStatus.Published, recipe.Status);
    }

    [Fact]
    public void Publish_ConvertsThePublicationTimestampToUtc()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.FromHours(7));
        var recipe = CreateReadyRecipe();

        recipe.Publish(publishedAt);

        Assert.Equal(publishedAt.ToUniversalTime(), recipe.PublishedAt);
        Assert.Equal(TimeSpan.Zero, recipe.PublishedAt?.Offset);
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_PreservesTheExistingTimestamp()
    {
        var firstPublishedAt = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
        var recipe = CreateReadyRecipe();
        recipe.Publish(firstPublishedAt);

        recipe.Publish(firstPublishedAt.AddDays(1));

        Assert.Equal(firstPublishedAt, recipe.PublishedAt);
    }

    [Fact]
    public void Publish_AfterArchive_UpdatesThePublicationTimestamp()
    {
        var firstPublishedAt = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
        var republishedAt = firstPublishedAt.AddDays(1);
        var recipe = CreateReadyRecipe();
        recipe.Publish(firstPublishedAt);

        recipe.Status = RecipeStatus.Archived;
        recipe.Publish(republishedAt);

        Assert.Equal(republishedAt, recipe.PublishedAt);
        Assert.Equal(RecipeStatus.Published, recipe.Status);
    }

    private static Recipe CreateReadyRecipe()
    {
        var recipe = new Recipe();
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Flour" });
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });
        return recipe;
    }
}
