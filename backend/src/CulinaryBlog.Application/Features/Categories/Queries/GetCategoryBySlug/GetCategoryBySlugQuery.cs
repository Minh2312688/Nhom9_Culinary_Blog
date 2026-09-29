using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;

/// <summary>FR-CAT-002: lấy một category theo slug (không kèm danh sách Recipe).</summary>
public sealed record GetCategoryBySlugQuery(string Slug) : IRequest<CategoryDto>;

public sealed class GetCategoryBySlugQueryValidator : AbstractValidator<GetCategoryBySlugQuery>
{
    public GetCategoryBySlugQueryValidator()
    {
        RuleFor(query => query.Slug)
            .NotEmpty()
                .WithMessage("Slug is required.")
            .MaximumLength(120)
                .WithMessage("Slug must not exceed 120 characters.");
    }
}

public sealed class GetCategoryBySlugQueryHandler
    : IRequestHandler<GetCategoryBySlugQuery, CategoryDto>
{
    private readonly IApplicationDbContext context;
    private readonly ICategoryCache cache;

    public GetCategoryBySlugQueryHandler(IApplicationDbContext context, ICategoryCache cache)
    {
        this.context = context;
        this.cache = cache;
    }

    public async Task<CategoryDto> Handle(
        GetCategoryBySlugQuery request,
        CancellationToken cancellationToken)
    {
        // Slug lưu trong database luôn lowercase. Chuẩn hoá MỘT lần rồi dùng cho cả cache
        // key và truy vấn EF, nếu không cache hit và cache miss sẽ trả kết quả khác nhau
        // (ví dụ "BANH-NGOT" hit cache trong khi vẫn trả 404 khi miss).
        var slug = request.Slug.Trim().ToLowerInvariant();

        var cacheKey = CategoryCacheKeys.ForSlug(slug);
        var cached = await cache.GetAsync<CategoryDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        // Global query filter của EF loại bỏ category đã soft delete
        var category = await context.Categories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Slug == slug, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), slug);

        var result = category.Adapt<CategoryDto>();
        await cache.SetAsync(cacheKey, result, CategoryCacheKeys.Ttl, cancellationToken);
        return result;
    }
}