namespace CulinaryBlog.Domain.Entities;

public class RecipeStep : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int StepNumber { get; set; }
    public string? Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? TimerMinutes { get; set; }
    public string? ImageUrl { get; set; }
}