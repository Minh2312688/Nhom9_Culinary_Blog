using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record AddRecipeStepCommand(
    Guid RecipeId,
    string Title,
    string Description,
    int? DurationMinutes,
    string? ImageUrl) : IRequest<RecipeStepDto>;

public sealed class AddRecipeStepCommandValidator : AbstractValidator<AddRecipeStepCommand>
{
    public AddRecipeStepCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
    }
}

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

public sealed record UpdateRecipeStepCommand(
    Guid RecipeId,
    Guid StepId,
    string Title,
    string Description,
    int? DurationMinutes,
    string? ImageUrl) : IRequest<RecipeStepDto>;

public sealed class UpdateRecipeStepCommandValidator : AbstractValidator<UpdateRecipeStepCommand>
{
    public UpdateRecipeStepCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
    }
}

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

internal static class RecipeStepCommandHelpers
{
    public static async Task<Recipe> GetEditableRecipeAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || (currentUser.UserId is null && !currentUser.IsAdmin))
            throw new ForbiddenAccessException("Authentication is required.");

        var recipe = await context.Recipes.SingleOrDefaultAsync(x => x.Id == recipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), recipeId);
        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId)
            throw new ForbiddenAccessException();
        return recipe;
    }

    public static async Task InvalidateAsync(IRecipeCache cache, string slug, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"recipes:slug:{slug}", cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
    }

    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static RecipeStepDto ToStepDto(this RecipeStep step) => new(
        step.Id, step.StepNumber, step.Title, step.Description, step.DurationMinutes, step.ImageUrl);
}
