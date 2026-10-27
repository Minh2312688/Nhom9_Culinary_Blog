namespace CulinaryBlog.Application.Common.Security;

/// <summary>
/// Validates and enforces CORS allowed origins per SRS v1.0.0 NFR-SEC-005:
/// - Explicit configured origins only.
/// - Wildcards ('*' or '*.domain') are strictly prohibited in all environments.
/// - Validates absolute HTTP/HTTPS URI scheme.
/// </summary>
public static class CorsPolicyValidator
{
    public static string[] ValidateAndFilterAllowedOrigins(string[]? configuredOrigins, bool isDevelopmentOrTesting)
    {
        if (configuredOrigins is null || configuredOrigins.Length == 0)
        {
            return isDevelopmentOrTesting
                ? new[] { "http://localhost:3000", "http://localhost:3001" }
                : Array.Empty<string>();
        }

        var validated = new List<string>();

        foreach (var rawOrigin in configuredOrigins)
        {
            if (string.IsNullOrWhiteSpace(rawOrigin))
            {
                continue;
            }

            var origin = rawOrigin.Trim();

            // SRS NFR-SEC-005: Wildcard origins ('*') are strictly prohibited
            if (origin == "*" || origin.Contains('*'))
            {
                throw new InvalidOperationException(
                    $"CORS origin '{origin}' is invalid. Wildcard origins are strictly prohibited by SRS v1.0.0 NFR-SEC-005.");
            }

            // Must be absolute HTTP or HTTPS URI
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"CORS origin '{origin}' is invalid. Allowed origins must be valid absolute HTTP or HTTPS URLs.");
            }

            validated.Add(origin.TrimEnd('/'));
        }

        return validated.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
