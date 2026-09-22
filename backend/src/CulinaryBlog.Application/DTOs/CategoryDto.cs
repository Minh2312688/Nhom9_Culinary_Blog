namespace CulinaryBlog.Application.DTOs;
// DTO — chỉ chứa data, không có logic
// Mapster sẽ tự động map từ Category Entity sang CategoryDto
// nhờ tên property trùng khớp (convention-based mapping)
public record CategoryDto(
 Guid Id,
 string Name,
 string Slug,
 string? Description,
 DateTime CreatedAt
);
