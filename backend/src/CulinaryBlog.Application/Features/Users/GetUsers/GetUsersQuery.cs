using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Users;
using MediatR;

namespace CulinaryBlog.Application.Features.Users.GetUsers;

public sealed record GetUsersQuery(
    int Page = 1,
    int PageSize = 10,
    string? Role = null,
    string? SearchTerm = null) : IRequest<PaginatedResult<UserListItemDto>>;

