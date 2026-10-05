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

        Assert.Throws<DomainRuleViolationException>(recipe.Publish);
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }

    [Fact]
    public void Publish_WithIngredientAndStep_ChangesStatus()
    {
        var recipe = new Recipe();
        recipe.Ingredients.Add(new RecipeIngredient { Name = "Flour" });
        recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });

        recipe.Publish();

        Assert.Equal(RecipeStatus.Published, recipe.Status);
    }
}
