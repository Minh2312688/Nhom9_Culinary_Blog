namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
