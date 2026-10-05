using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Profile;

public sealed record GetCurrentUserQuery(string UserId) : IRequest<UserProfileDto>;
