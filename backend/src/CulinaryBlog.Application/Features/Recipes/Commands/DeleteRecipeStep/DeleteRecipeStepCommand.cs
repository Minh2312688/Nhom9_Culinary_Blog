using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record DeleteRecipeStepCommand(Guid RecipeId, Guid StepId) : IRequest;

public sealed class DeleteRecipeStepCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<DeleteRecipeStepCommand>
{
    public async Task Handle(DeleteRecipeStepCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeStepCommandHelpers.GetEditableRecipeAsync(context, currentUser, request.RecipeId, cancellationToken);
        var allSteps = await context.RecipeSteps.IgnoreQueryFilters()
            .Where(x => x.RecipeId == recipe.Id)
            .OrderBy(x => x.StepNumber)
            .ToListAsync(cancellationToken);
        var target = allSteps.SingleOrDefault(x => x.Id == request.StepId && !x.IsDeleted)
            ?? throw new NotFoundException(nameof(RecipeStep), request.StepId);

        var activeSteps = allSteps.Where(x => !x.IsDeleted && x.Id != target.Id)
            .OrderBy(x => x.StepNumber)
            .ToList();
        var minimumStepNumber = allSteps.Count == 0 ? 0 : allSteps.Min(x => x.StepNumber);
        var nextParkedStepNumber = Math.Min(-1L, (long)minimumStepNumber - 1);
        var deletedSteps = allSteps.Where(x => x.IsDeleted).ToList();
        if (nextParkedStepNumber - deletedSteps.Count < int.MinValue)
            throw new ConflictException("Deleted recipe steps cannot be safely renumbered.");

        foreach (var deletedStep in deletedSteps)
            deletedStep.StepNumber = (int)nextParkedStepNumber--;
        target.IsDeleted = true;
        target.StepNumber = (int)nextParkedStepNumber;
        await context.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < activeSteps.Count; i++)
        {
            var expectedNumber = i + 1;
            if (activeSteps[i].StepNumber == expectedNumber)
                continue;

            activeSteps[i].StepNumber = expectedNumber;
            await context.SaveChangesAsync(cancellationToken);
        }

        await RecipeStepCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
    }
}
