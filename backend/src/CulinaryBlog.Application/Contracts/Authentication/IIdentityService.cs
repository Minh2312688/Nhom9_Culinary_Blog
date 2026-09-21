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
}
