namespace CulinaryBlog.Application.DTOs;
// DTO — chỉ chứa data, không có logic
// Mapster sẽ tự động map từ Category Entity sang CategoryDto
// nhờ tên property trùng khớp (convention-based mapping)
public record CategoryDto(
 Guid Id,
 string Name,
 string Slug,
 string? Description,
 string? ImageUrl,
 int OrderIndex,
 DateTime CreatedAt
)
{
 // FR-CAT-001: số Recipe Published của category (không tính Draft/Archived và không tính
 // recipe đã soft delete). Không phải tham số primary constructor để giữ projection Mapster
 // của list chỉ SELECT các cột có sẵn ở entity; handler gán sau bằng một truy vấn group by.
 public int RecipeCount { get; init; }
};
