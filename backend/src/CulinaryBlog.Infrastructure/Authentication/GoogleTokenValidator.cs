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
        try
        {
            var clientId = _configuration["Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                settings.Audience = new[] { clientId };
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (payload == null) return null;

            return new GoogleTokenInfo(
                Subject: payload.Subject,
                Email: payload.Email,
                Name: payload.Name ?? payload.Email,
                Picture: payload.Picture);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
