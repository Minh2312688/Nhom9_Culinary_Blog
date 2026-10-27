using CulinaryBlog.Application.Common.Validation;
using CulinaryBlog.Domain.Entities;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200).MustNotContainHtmlMarkup();
        RuleFor(x => x.Description).MustNotContainHtmlMarkup();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PrepTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CookTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.Difficulty).Must(IsSupportedDifficulty)
            .WithMessage("Difficulty must be Easy, Medium, Hard, or Expert.");
        RuleForEach(x => x.Ingredients).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty().MaximumLength(100).MustNotContainHtmlMarkup();
            item.RuleFor(i => i.Quantity).GreaterThan(0).When(i => i.Quantity.HasValue);
            item.RuleFor(i => i.Notes).MaximumLength(200).MustNotContainHtmlMarkup();
        });
        RuleForEach(x => x.Steps).ChildRules(item =>
        {
            item.RuleFor(i => i.Title).MaximumLength(200).MustNotContainHtmlMarkup();
            item.RuleFor(i => i.Description).NotEmpty().MustNotContainHtmlMarkup();
            item.RuleFor(i => i.DurationMinutes).GreaterThanOrEqualTo(0);
        });
    }

    private static bool IsSupportedDifficulty(string value) =>
        Enum.TryParse<RecipeDifficulty>(value, true, out var difficulty) &&
        Enum.IsDefined(difficulty) &&
        string.Equals(difficulty.ToString(), value, StringComparison.OrdinalIgnoreCase);
}
