using FluentValidation;

namespace CulinaryBlog.Application.System.Queries.GetBaseStatus;

public sealed class GetBaseStatusQueryValidator : AbstractValidator<GetBaseStatusQuery>
{
    public GetBaseStatusQueryValidator()
    {
        RuleFor(request => request.Environment)
            .NotEmpty()
            .MaximumLength(100);
    }
}
