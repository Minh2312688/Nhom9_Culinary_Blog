using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.TokenRefresh;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.")
            .MaximumLength(512).WithMessage("Refresh token must not exceed 512 characters.");
    }
}
