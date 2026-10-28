namespace CulinaryBlog.Application.DTOs.Users;

public sealed record UserListItemDto(
    string Id,
    string? Email,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    IList<string> Roles);

