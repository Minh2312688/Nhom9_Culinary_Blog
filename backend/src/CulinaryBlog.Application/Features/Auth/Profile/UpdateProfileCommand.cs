using CulinaryBlog.Application.DTOs.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Profile;

/// <summary>
/// FR-AUTH-007: Command to update profile fields for the currently authenticated user.
/// </summary>
public sealed record UpdateProfileCommand(
    string CurrentUserId,
    string? DisplayName,
    string? AvatarUrl,
    string? Bio) : IRequest<UserProfileDto>;
