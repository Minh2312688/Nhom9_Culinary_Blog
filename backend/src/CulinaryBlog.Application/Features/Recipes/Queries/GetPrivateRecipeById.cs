using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries;

public sealed record GetPrivateRecipeByIdQuery(Guid Id) : IRequest<RecipeDetailDto>;

public sealed class GetPrivateRecipeByIdQueryHandler(
    IRecipeRepository recipes,
    ICurrentUserService currentUser) : IRequestHandler<GetPrivateRecipeByIdQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(
        GetPrivateRecipeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var recipe = await recipes.Query.AsNoTracking().AsSplitQuery()
            .Include(item => item.Ingredients)
            .Include(item => item.Steps)
            .Include(item => item.Images)
            .Include(item => item.Nutrition)
            .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.Id);

        if (!currentUser.IsAdmin &&
            (!currentUser.IsAuthenticated || recipe.AuthorId != currentUser.UserId))
        {
            throw new ForbiddenAccessException();
        }

        return recipe.ToDetail();
    }
}
