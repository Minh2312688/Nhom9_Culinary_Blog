using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record UpdateRecipeStepCommand(
    Guid RecipeId,
    Guid StepId,
    string Title,
    string Description,
    int? DurationMinutes,
    string? ImageUrl) : IRequest<RecipeStepDto>;

public sealed class UpdateRecipeStepCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<UpdateRecipeStepCommand, RecipeStepDto>
{
    public async Task<RecipeStepDto> Handle(UpdateRecipeStepCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeStepCommandHelpers.GetEditableRecipeAsync(context, currentUser, request.RecipeId, cancellationToken);
        var step = await context.RecipeSteps.SingleOrDefaultAsync(
            x => x.Id == request.StepId && x.RecipeId == recipe.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(RecipeStep), request.StepId);

        step.Title = request.Title.Trim();
        step.Description = request.Description.Trim();
        step.DurationMinutes = request.DurationMinutes;
        step.ImageUrl = RecipeStepCommandHelpers.TrimToNull(request.ImageUrl);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeStepCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
        return step.ToStepDto();
    }
}
