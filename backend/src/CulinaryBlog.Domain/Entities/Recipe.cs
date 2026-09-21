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
    public int Difficulty { get; set; } = 1; // 1=Easy, 2=Medium, 3=Hard, 4=Expert
    public int Status { get; set; } = 1; // 0=Draft, 1=Published, 2=Archived

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
}
