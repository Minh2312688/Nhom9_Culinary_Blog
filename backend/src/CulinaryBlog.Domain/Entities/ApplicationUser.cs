using CulinaryBlog.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Domain.Entities;

public class ApplicationUser : IdentityUser<string>, IHasRole
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Role { get; set; } = nameof(UserRole.Reader);

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}