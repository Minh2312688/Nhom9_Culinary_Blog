namespace CulinaryBlog.Application.Contracts.Authentication;

public sealed record RegisterResult(
    bool Succeeded,
    string? UserId,
    string? Email,
    string? DisplayName,
    string? ErrorMessage,
    bool IsDuplicateEmail = false);

public sealed record LoginResult(
    bool Succeeded,
    string? UserId,
    string? Email,
    IList<string>? Roles,
    bool IsLockedOut = false,
    DateTimeOffset? LockoutEnd = null,
    string? ErrorMessage = null);

public sealed record GoogleAuthResult(
    bool Succeeded,
    string? UserId,
    string? Email,
    IList<string>? Roles,
    string? ErrorMessage = null,
    bool IsInvalidToken = false);

public sealed record UserProfileResult(
    bool Succeeded,
    string? UserId,
    string? Email,
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    bool IsActive,
    bool IsLockedOut,
    IList<string>? Roles);

public sealed record ChangePasswordResult(
    bool Succeeded,
    string? ErrorMessage = null,
    bool IsIncorrectCurrentPassword = false);

public sealed record ForgotPasswordResult(
    bool Succeeded,
    string? ResetToken = null,
    string? ErrorMessage = null);

public sealed record ResetPasswordResult(
    bool Succeeded,
    string? ErrorMessage = null,
    bool IsInvalidToken = false);

public interface IIdentityService
{
    Task<RegisterResult> RegisterUserAsync(
        string email,
        string password,
        string displayName,
        string role,
        CancellationToken cancellationToken = default);

    Task<LoginResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<GoogleAuthResult> AuthenticateGoogleUserAsync(
        string idToken,
        string defaultRole,
        CancellationToken cancellationToken = default);

    Task<UserProfileResult?> GetUserProfileByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<UserProfileResult?> UpdateUserProfileAsync(
        string userId,
        string? displayName,
        string? avatarUrl,
        string? bio,
        CancellationToken cancellationToken = default);

    Task<ChangePasswordResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<ForgotPasswordResult> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<ResetPasswordResult> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
