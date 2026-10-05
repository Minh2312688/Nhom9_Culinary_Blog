using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Profile;

/// <summary>
/// FR-AUTH-007: Handler for updating current user profile.
/// </summary>
public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    private readonly IIdentityService _identityService;

    public UpdateProfileCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<UserProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userProfile = await _identityService.UpdateUserProfileAsync(
            request.CurrentUserId,
            request.DisplayName,
            request.AvatarUrl,
            request.Bio,
            cancellationToken);

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
