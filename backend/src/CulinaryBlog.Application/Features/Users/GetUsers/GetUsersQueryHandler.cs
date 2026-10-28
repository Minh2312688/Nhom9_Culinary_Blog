using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Users;
using MediatR;

namespace CulinaryBlog.Application.Features.Users.GetUsers;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PaginatedResult<UserListItemDto>>
{
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<PaginatedResult<UserListItemDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        return await _identityService.GetUsersAsync(
            request.Page,
            request.PageSize,
            request.Role,
            request.SearchTerm,
            cancellationToken);
    }
}

