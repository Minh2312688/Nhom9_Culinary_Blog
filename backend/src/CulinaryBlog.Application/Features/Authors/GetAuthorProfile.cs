using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Authors;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Authors;

public sealed record GetAuthorProfileQuery(string AuthorId, int Page = 1, int PageSize = 12)
    : IRequest<AuthorProfilePageDto>;

public sealed class GetAuthorProfileQueryValidator : AbstractValidator<GetAuthorProfileQuery>
{
    public GetAuthorProfileQueryValidator()
    {
        RuleFor(query => query.AuthorId).NotEmpty().MaximumLength(450);
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetAuthorProfileQueryHandler(
    IIdentityService identityService,
    IRecipeRepository recipes) : IRequestHandler<GetAuthorProfileQuery, AuthorProfilePageDto>
{
    public async Task<AuthorProfilePageDto> Handle(
        GetAuthorProfileQuery request,
        CancellationToken cancellationToken)
    {
        var author = await identityService.GetUserProfileByIdAsync(request.AuthorId, cancellationToken);
        if (author is not { Succeeded: true, IsActive: true, UserId: not null, DisplayName: not null })
            throw new NotFoundException("Author", request.AuthorId);

        var query = recipes.Query.AsNoTracking()
            .Where(recipe => recipe.AuthorId == request.AuthorId && recipe.Status == RecipeStatus.Published);
        var totalCount = await query.CountAsync(cancellationToken);
        var pageItems = await query
            .OrderBy(recipe => recipe.PublishedAt == null)
            .ThenByDescending(recipe => recipe.PublishedAt)
            .ThenBy(recipe => recipe.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<RecipeSummaryDto>(
            pageItems.Select(recipe => recipe.ToSummary()).ToList(),
            totalCount,
            request.Page,
            request.PageSize);

        return new AuthorProfilePageDto(
            author.UserId,
            author.DisplayName,
            author.AvatarUrl,
            author.Bio,
            result);
    }
}
