using CulinaryBlog.Application.Common.Validation;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed class UpdateRecipeIngredientCommandValidator : AbstractValidator<UpdateRecipeIngredientCommand>
{
    public UpdateRecipeIngredientCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.IngredientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).MustNotContainHtmlMarkup();
        RuleFor(x => x.Quantity).Must(x => x is null or > 0).WithMessage("Quantity must be greater than zero when provided.");
        RuleFor(x => x.Unit).MaximumLength(50).MustNotContainHtmlMarkup();
        RuleFor(x => x.Notes).MaximumLength(200).MustNotContainHtmlMarkup();
        RuleFor(x => x.OrderIndex).NotNull().GreaterThanOrEqualTo(0);
    }
}
