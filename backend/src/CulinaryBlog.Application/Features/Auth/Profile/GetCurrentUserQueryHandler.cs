using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Profile;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserProfileDto>
{
    private readonly IIdentityService _identityService;

    public GetCurrentUserQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<UserProfileDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userProfile = await _identityService.GetUserProfileByIdAsync(request.UserId, cancellationToken);

        if (userProfile == null)
        {
            throw new NotFoundException("User not found.");
        }

        return new UserProfileDto(
            Id: userProfile.UserId!,
            Email: userProfile.Email!,
            DisplayName: userProfile.DisplayName ?? userProfile.Email!,
            AvatarUrl: userProfile.AvatarUrl,
            Bio: userProfile.Bio,
            EmailConfirmed: userProfile.EmailConfirmed,
            CreatedAt: userProfile.CreatedAt,
            Roles: userProfile.Roles ?? Array.Empty<string>());
    }
}
