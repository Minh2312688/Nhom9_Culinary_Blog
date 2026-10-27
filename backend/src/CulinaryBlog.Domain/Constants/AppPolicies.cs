namespace CulinaryBlog.Domain.Constants;

/// <summary>
/// Centralized authorization policy names per SRS v1.0.0 NFR-SEC-006.
/// Avoids hardcoded policy/role strings across presentation and application layers.
/// </summary>
public static class AppPolicies
{
    /// <summary>
    /// Grants access to Author or Admin roles.
    /// </summary>
    public const string AuthorPolicy = "AuthorPolicy";

    /// <summary>
    /// Grants access strictly to Admin role.
    /// </summary>
    public const string AdminPolicy = "AdminPolicy";
}
