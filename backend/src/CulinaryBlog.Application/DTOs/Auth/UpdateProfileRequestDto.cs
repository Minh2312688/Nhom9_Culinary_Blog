namespace CulinaryBlog.Application.DTOs.Auth;

/// <summary>
/// FR-AUTH-007: Request payload for PATCH /api/v1/auth/me
/// Semantics: Fields are optional. Only supplied non-null fields will be updated.
/// Does NOT allow updating Email, UserName, Password, or Roles.
/// </summary>
public sealed record UpdateProfileRequestDto(
    string? DisplayName,
    string? AvatarUrl,
    string? Bio);
