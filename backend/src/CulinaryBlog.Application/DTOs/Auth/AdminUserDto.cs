namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record AdminUserDto(
    string Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    bool IsActive,
    bool IsLockedOut,
    IList<string> Roles);
