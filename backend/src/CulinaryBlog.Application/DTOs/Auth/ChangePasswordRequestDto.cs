namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);
