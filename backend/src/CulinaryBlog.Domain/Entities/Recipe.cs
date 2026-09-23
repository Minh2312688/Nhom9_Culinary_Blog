using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

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
    public string Difficulty { get; set; } = "Easy";
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    public ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();
    public RecipeNutrition? Nutrition { get; set; }
}
