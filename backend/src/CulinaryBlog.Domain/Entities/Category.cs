namespace CulinaryBlog.Domain.Entities;
public class Category
{
 public Guid Id { get; private set; }
 public string Name { get; private set; } = string.Empty;
 public string Slug { get; private set; } = string.Empty;
 public string? Description { get; private set; }
 public DateTime CreatedAt { get; private set; }
 public DateTime? UpdatedAt { get; private set; }
 protected Category() { }
 public static Category Create(string name, string? description = null)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
 return new Category
 {
 Id = Guid.NewGuid(),
 Name = name.Trim(),
 Slug = GenerateSlug(name),
 Description = description?.Trim(),
 CreatedAt = DateTime.UtcNow,
 };
 }
 public void Update(string name, string? description)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
 Name = name.Trim();
 Slug = GenerateSlug(name);
 Description = description?.Trim();
 UpdatedAt = DateTime.UtcNow;
 }
 // Tạo URL slug từ tên: "Món Tráng Miệng" → "mon-trang-mieng"
 private static string GenerateSlug(string name)
 {
 return name.ToLowerInvariant()
 .Normalize(System.Text.NormalizationForm.FormD)
 .Replace(" ", "-")
 .Replace("đ", "d")
 .Replace("Đ", "d");
 // Lưu ý: Production nên dùng package Slugify.Core để xử lý Unicode đầy đủ
 }
}