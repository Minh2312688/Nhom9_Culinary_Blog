using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record AddRecipeStepCommand(
    Guid RecipeId,
    string Title,
    string Description,
    int? DurationMinutes,
    string? ImageUrl) : IRequest<RecipeStepDto>;

public sealed class AddRecipeStepCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<AddRecipeStepCommand, RecipeStepDto>
{
    public async Task<RecipeStepDto> Handle(AddRecipeStepCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeStepCommandHelpers.GetEditableRecipeAsync(context, currentUser, request.RecipeId, cancellationToken);
        var maxStepNumber = await context.RecipeSteps.IgnoreQueryFilters()
            .Where(x => x.RecipeId == recipe.Id)
            .Select(x => (int?)x.StepNumber)
            .MaxAsync(cancellationToken) ?? 0;
        if (maxStepNumber == int.MaxValue)
            throw new ConflictException("No further step number can be assigned to this recipe.");

        var step = new RecipeStep
        {
            RecipeId = recipe.Id,
            StepNumber = maxStepNumber + 1,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            DurationMinutes = request.DurationMinutes,
            ImageUrl = RecipeStepCommandHelpers.TrimToNull(request.ImageUrl)
        };
        context.RecipeSteps.Add(step);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeStepCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
        return step.ToStepDto();
    }
}
