namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record UserProfileDto(
    string Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    IList<string> Roles);
