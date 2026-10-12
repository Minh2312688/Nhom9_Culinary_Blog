using CulinaryBlog.Application.Common.Validation;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.Profile;

/// <summary>
/// FR-AUTH-007: Validator for UpdateProfileCommand.
/// </summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.CurrentUserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        // At least one field must be provided in PATCH body
        RuleFor(x => x)
            .Must(x => x.DisplayName != null || x.AvatarUrl != null || x.Bio != null)
            .WithMessage("At least one field (displayName, avatarUrl, or bio) must be provided for update.");

        When(x => x.DisplayName != null, () =>
        {
            RuleFor(x => x.DisplayName)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("DisplayName cannot be empty or whitespace.")
                .Length(2, 100)
                .WithMessage("DisplayName must be between 2 and 100 characters.")
                .MustNotContainHtmlMarkup();
        });

        When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl), () =>
        {
            RuleFor(x => x.AvatarUrl)
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var result) &&
                             (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                .WithMessage("AvatarUrl must be a valid HTTP or HTTPS URL.")
                .MaximumLength(2048)
                .WithMessage("AvatarUrl must not exceed 2048 characters.");
        });

        When(x => x.Bio != null, () =>
        {
            RuleFor(x => x.Bio)
                .MaximumLength(500)
                .WithMessage("Bio must not exceed 500 characters.")
                .MustNotContainHtmlMarkup();
        });
    }
}
