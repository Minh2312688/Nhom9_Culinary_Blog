using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Authentication;

public sealed record GoogleTokenInfo(
    string Subject,
    string Email,
    string Name,
    string? Picture);

public interface IGoogleTokenValidator
{
    Task<GoogleTokenInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IConfiguration _configuration;

    public GoogleTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<GoogleTokenInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var clientId = _configuration["Google:ClientId"];
        var settings = BuildValidationSettings(clientId);
        if (settings == null)
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return ValidatePayload(payload);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static GoogleJsonWebSignature.ValidationSettings? BuildValidationSettings(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        return new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { clientId.Trim() }
        };
    }

    public static GoogleTokenInfo? ValidatePayload(GoogleJsonWebSignature.Payload? payload)
    {
        if (payload == null)
        {
            return null;
        }

        if (!payload.EmailVerified)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
        {
            return null;
        }

        return new GoogleTokenInfo(
            Subject: payload.Subject,
            Email: payload.Email,
            Name: string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
            Picture: payload.Picture);
    }
}
