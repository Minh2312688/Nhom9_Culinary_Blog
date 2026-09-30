using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;

    public void Revoke(DateTimeOffset revokedAt, string? replacedByTokenHash = null)
    {
        if (RevokedAt != null)
        {
            throw new InvalidTokenStateException("Refresh token has already been revoked.");
        }

        RevokedAt = revokedAt;
        if (!string.IsNullOrEmpty(replacedByTokenHash))
        {
            ReplacedByTokenHash = replacedByTokenHash;
        }
    }
}
