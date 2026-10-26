namespace CulinaryBlog.Application.DTOs.Auth;

public sealed record ForgotPasswordRequestDto(
    string Email);

public sealed record ResetPasswordRequestDto(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword);
