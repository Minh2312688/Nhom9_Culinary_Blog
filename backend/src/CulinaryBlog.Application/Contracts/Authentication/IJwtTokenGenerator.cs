namespace CulinaryBlog.Application.Contracts.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(string userId, string email, IEnumerable<string> roles);

    (string rawToken, string tokenHash) GenerateRefreshToken();
}
