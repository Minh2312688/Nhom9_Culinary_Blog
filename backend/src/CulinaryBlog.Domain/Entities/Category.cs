using System.Globalization;
using System.Text;

namespace CulinaryBlog.Domain.Entities;

public class Category : BaseEntity
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const int MaxImageUrlLength = 1000;
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int OrderIndex { get; set; } = 0;

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

    public static Category Create(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        Validate(normalizedName, description, null);
        return new Category
        {
            Name = normalizedName,
            Slug = CreateSlug(normalizedName),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };
    }

    public void Update(string name, string? description) =>
        Update(name, description, ImageUrl, OrderIndex);

    public void Update(string name, string? description, string? imageUrl, int orderIndex)
    {
        var normalizedName = name?.Trim();
        Validate(normalizedName, description, imageUrl);
        if (orderIndex < 0) throw new ArgumentOutOfRangeException(nameof(orderIndex));

        Name = normalizedName!;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        OrderIndex = orderIndex;
        UpdatedAt = DateTime.UtcNow;
    }

    public static bool CanCreateSlug(string? value) =>
        !string.IsNullOrWhiteSpace(value) && CreateSlug(value).Length > 0;

    private static void Validate(string? name, string? description, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < MinNameLength || name.Length > MaxNameLength)
            throw new ArgumentException($"Name must contain {MinNameLength}-{MaxNameLength} characters.", nameof(name));
        if (description?.Trim().Length > MaxDescriptionLength)
            throw new ArgumentException($"Description must not exceed {MaxDescriptionLength} characters.", nameof(description));
        if (imageUrl?.Trim().Length > MaxImageUrlLength)
            throw new ArgumentException($"ImageUrl must not exceed {MaxImageUrlLength} characters.", nameof(imageUrl));
        if (!CanCreateSlug(name)) throw new ArgumentException("Name must contain a letter or digit.", nameof(name));
    }

    private static string CreateSlug(string value)
    {
        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSeparator = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
