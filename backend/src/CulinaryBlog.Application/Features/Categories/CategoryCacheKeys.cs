using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;

namespace CulinaryBlog.Application.Features.Categories;

/// <summary>
/// Contract cache của nhóm: Category dùng Redis distributed cache với TTL 30 phút.
/// Key phải chứa đầy đủ mọi tham số query ảnh hưởng kết quả để không trả nhầm dữ liệu.
/// </summary>
public static class CategoryCacheKeys
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public static string ForList(GetCategoriesQuery query)
    {
        var search = string.IsNullOrWhiteSpace(query.Search)
            ? "-"
            : query.Search.Trim().ToLowerInvariant();

        return $"categories:list:p={query.Page};ps={query.PageSize};s={search};" +
            $"sb={Normalize(query.SortBy)};so={Normalize(query.SortOrder)}";
    }

    public static string ForSlug(string slug) => $"categories:slug:{Normalize(slug)}";

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}