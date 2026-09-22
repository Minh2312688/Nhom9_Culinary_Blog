using System.Security.Claims;
using CulinaryBlog.Application.Contracts;

namespace CulinaryBlog.API;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
    public string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    public bool IsAdmin => User.IsInRole("Admin") || User.FindFirstValue("role") == "Admin";
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
}
