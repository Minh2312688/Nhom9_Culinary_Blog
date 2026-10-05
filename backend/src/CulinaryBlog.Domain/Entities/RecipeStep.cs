namespace CulinaryBlog.Domain.Entities;

public class RecipeStep : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int StepNumber { get; set; }
    public string? Title { get; set; }
    public string Description { get; set; } = default!;
    public int? DurationMinutes { get; set; }
    public string? ImageUrl { get; set; }
}
