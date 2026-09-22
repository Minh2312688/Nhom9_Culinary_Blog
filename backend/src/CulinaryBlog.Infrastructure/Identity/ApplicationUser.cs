using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Identity;

/// <summary>
/// TEMPORARY IMPLEMENTATION CHOICE
/// CONFLICT-025 REMAINS OPEN.
/// ApplicationUser extends IdentityUser<string> placed in Infrastructure to keep Domain pure.
/// </summary>
public class ApplicationUser : IdentityUser<string>
{
    public string DisplayName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
