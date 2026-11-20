using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed class UploadRecipeImageCommandValidator : AbstractValidator<UploadRecipeImageCommand>
{
    public UploadRecipeImageCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AltText).MaximumLength(200);
        RuleFor(x => x.StepNumber)
            .GreaterThan(0)
            .When(x => x.StepNumber.HasValue);
    }
}
