namespace CulinaryBlog.Domain.Entities;

using CulinaryBlog.Domain.Exceptions;

public class Recipe : BaseEntity
{
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public string AuthorId { get; set; } = default!;

    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }
    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.Easy;
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    public ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();
    public RecipeNutrition? Nutrition { get; set; }

    public void Publish()
    {
        if (Ingredients.Count == 0 || Steps.Count == 0)
            throw new DomainRuleViolationException("A recipe must have at least one step and one ingredient before publishing.");

        Status = RecipeStatus.Published;
    }
}
