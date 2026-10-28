using FluentValidation;

namespace CulinaryBlog.Application.Features.Users.GetUsers;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("PageSize must be between 1 and 50.");

        RuleFor(x => x.SearchTerm)
            .MaximumLength(100)
            .WithMessage("SearchTerm must not exceed 100 characters.");

        RuleFor(x => x.Role)
            .MaximumLength(50)
            .WithMessage("Role must not exceed 50 characters.");
    }
}

