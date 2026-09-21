namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record RegisterResponseDto(
    string UserId,
    string Email,
    string DisplayName);
