using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;

/// <summary>
/// Validate tham số phân trang của GetCategoriesQuery.
/// Search/SortBy giữ nguyên hành vi hiện có của handler (search không phân biệt hoa thường,
/// SortBy không nhận diện được sẽ rơi về sắp xếp theo Name) nên chưa thêm ràng buộc mới.
/// </summary>
public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
                .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
                .WithMessage("PageSize must be greater than or equal to 1.")
            .LessThanOrEqualTo(GetCategoriesQuery.MaxPageSize)
                .WithMessage($"PageSize must not exceed {GetCategoriesQuery.MaxPageSize}.");
    }
}
