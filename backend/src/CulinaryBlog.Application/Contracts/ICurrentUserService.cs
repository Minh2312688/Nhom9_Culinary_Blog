namespace CulinaryBlog.Application.Contracts;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
}
