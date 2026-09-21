using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Domain.Constants;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResponseDto>
{
    private readonly IIdentityService _identityService;
    private readonly IWelcomeEmailEnqueuer _welcomeEmailEnqueuer;

    public RegisterCommandHandler(
        IIdentityService identityService,
        IWelcomeEmailEnqueuer welcomeEmailEnqueuer)
    {
        _identityService = identityService;
        _welcomeEmailEnqueuer = welcomeEmailEnqueuer;
    }

    public async Task<RegisterResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.RegisterUserAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            AppRoles.Author,
            cancellationToken);

        if (result.IsDuplicateEmail)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Registration failed.");
        }

        // FR-JOB-001 dependency: Enqueue welcome email (TV4)
        await _welcomeEmailEnqueuer.EnqueueWelcomeEmailAsync(
            result.Email!,
            result.DisplayName!,
            cancellationToken);

        return new RegisterResponseDto(
            result.UserId!,
            result.Email!,
            result.DisplayName!);
    }
}
