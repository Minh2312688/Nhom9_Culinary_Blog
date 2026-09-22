namespace CulinaryBlog.Domain.Entities;

public class Recipe : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }
    public string Difficulty { get; set; } = string.Empty;

    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    public ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();
    public RecipeNutrition? Nutrition { get; set; }
}
