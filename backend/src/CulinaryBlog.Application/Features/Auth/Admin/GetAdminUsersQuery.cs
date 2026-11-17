using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Admin;

public sealed record GetAdminUsersQuery : IRequest<IReadOnlyList<AdminUserDto>>;

public sealed class GetAdminUsersQueryHandler(IIdentityService identityService)
    : IRequestHandler<GetAdminUsersQuery, IReadOnlyList<AdminUserDto>>
{
    public async Task<IReadOnlyList<AdminUserDto>> Handle(
        GetAdminUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await identityService.GetAllUserProfilesAsync(cancellationToken);

        return users
            .Where(user => user.UserId is not null && user.Email is not null)
            .Select(user => new AdminUserDto(
                user.UserId!,
                user.Email!,
                user.DisplayName ?? user.Email!,
                user.EmailConfirmed,
                user.CreatedAt,
                user.IsActive,
                user.IsLockedOut,
                user.Roles ?? Array.Empty<string>()))
            .ToArray();
    }
}
