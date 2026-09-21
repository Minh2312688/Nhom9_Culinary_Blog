namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record RegisterRequestDto(
    string Email,
    string Password,
    string DisplayName);
