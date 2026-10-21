using CulinaryBlog.Application.Common.Validation;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed class UpdateRecipeStepCommandValidator : AbstractValidator<UpdateRecipeStepCommand>
{
    public UpdateRecipeStepCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).MustNotContainHtmlMarkup();
        RuleFor(x => x.Description).NotEmpty().MustNotContainHtmlMarkup();
        RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
    }
}
