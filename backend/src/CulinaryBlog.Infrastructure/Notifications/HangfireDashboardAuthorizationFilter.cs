using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CulinaryBlog.Domain.Constants;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure.Notifications;

/// <summary>
/// FR-JOB-001 / SRS-AUDIT §12.5: Admin Authorization Policy for the Hangfire
/// Dashboard at /hangfire.
///
/// Limits
/// ------
/// - This filter is the only gate for the Dashboard. No token is accepted from
///   the query string, no cookie or login flow is added, and no secret is read
///   from configuration. Cookies must not be used until the team defines a
///   safe bearer-token provisioning flow; the browser UI of the Dashboard is
///   therefore NOT considered usable by Admin until then.
/// - A valid JWT with role "Admin" is allowed; anonymous calls, calls with a
///   non-Admin role, expired/invalid tokens, and even a valid-admin token
///   supplied via query string are denied.
/// - JWT claims are validated with the existing "Jwt:Key/Issuer/Audience"
///   settings. Principal is in the Admin role via ClaimTypes.Role (it must not
///   be configration-dependent).
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private const string BearerPrefix = "Bearer ";

    private readonly TokenValidationParameters _validationParameters;

    /// <summary>
    /// Creates the filter from the same configuration section used by
    /// <see cref="DependencyInjection"/> (CONS-004 / NFR-SEC-002).
    /// </summary>
    public HangfireDashboardAuthorizationFilter(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Configuration 'Jwt:Key' is required and cannot be empty.");
        }

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "CulinaryBlog",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "CulinaryBlogApp",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    /// <summary>
    /// Used by unit tests only; does not validate Jwt:Key from configuration.
    /// </summary>
    internal HangfireDashboardAuthorizationFilter(TokenValidationParameters validationParameters)
    {
        _validationParameters = validationParameters;
    }

    public bool Authorize(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            return false;
        }

        var request = httpContext.Request;
        var authorizationHeader = request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        // Bearer-only: deliberate. No query-string token, no cookie, no
        // additional-auth scheme. The header must start with the literal
        // scheme, followed by the raw JWT between the space and the end.
        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorizationHeader[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return false;
        }

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, _validationParameters, out _);
            // JwtService issues ClaimTypes.Role; the check below matches that
            // shape and is not bound to a specific mapping of claim types.
            return principal.IsInRole(AppRoles.Admin)
                || principal.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == AppRoles.Admin);
        }
        catch (SecurityTokenException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
