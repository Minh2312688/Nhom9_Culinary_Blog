using CulinaryBlog.Domain.Common;
using System.Globalization;
using System.Text;

namespace CulinaryBlog.Domain.Entities;

public class Category : BaseEntity
{
    // Shared length limits used by the Domain guard clauses and the Application validators.
    public const int MinNameLength = 2;
    public const int MaxNameLength = 50;
    public const int MaxDescriptionLength = 500;
    public const int MaxImageUrlLength = 500;

    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int OrderIndex { get; set; } = 0;

    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

    public static Category Create(string name, string? description = null)
    {
        // Normalize first: invalid input must never leave a half-built entity behind.
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);
        return new Category
        {
            Name = normalizedName,
            Slug = CreateSlug(normalizedName),
            Description = normalizedDescription
        };
    }

    /// <summary>Update Name/Description, keeping the current ImageUrl and OrderIndex.</summary>
    public void Update(string name, string? description)
        => Update(name, description, ImageUrl, OrderIndex);

    /// <summary>
    /// Update Name, Description, ImageUrl and OrderIndex (CONFLICT-017).
    /// The slug is generated once in Create and is deliberately NOT regenerated, so renaming
    /// a category never breaks the existing /api/v1/categories/{slug} URL. All input is
    /// normalized before any field is assigned, so invalid input throws without leaving the
    /// entity partially modified.
    /// </summary>
    public void Update(string name, string? description, string? imageUrl, int orderIndex)
    {
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);
        var normalizedImageUrl = NormalizeImageUrl(imageUrl);

        Name = normalizedName;
        Description = normalizedDescription;
        ImageUrl = normalizedImageUrl;
        OrderIndex = orderIndex;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft delete (CONFLICT-002): the row is kept and the EF global query filter hides it
    /// from normal queries. No DeletedAt is added.
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length < MinNameLength)
        {
            throw new ArgumentException(
                $"Name must be at least {MinNameLength} characters long.", nameof(name));
        }

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Name must not exceed {MaxNameLength} characters.", nameof(name));
        }

        return trimmedName;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Description must not exceed {MaxDescriptionLength} characters.",
                nameof(description));
        }

        return trimmedDescription;
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var trimmedImageUrl = imageUrl.Trim();
        if (trimmedImageUrl.Length > MaxImageUrlLength)
        {
            throw new ArgumentException(
                $"ImageUrl must not exceed {MaxImageUrlLength} characters.",
                nameof(imageUrl));
        }

        return trimmedImageUrl;
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

        if (builder.Length == 0)
        {
            throw new ArgumentException(
                "Name must contain at least one letter or digit so a slug can be generated.",
                nameof(value));
        }

        return builder.ToString().Trim('-');
    }
}
