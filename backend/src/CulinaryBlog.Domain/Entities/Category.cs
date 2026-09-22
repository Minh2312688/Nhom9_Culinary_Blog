using System.Globalization;
using System.Text;

namespace CulinaryBlog.Domain.Entities;

public class Category
{
    // Giới hạn độ dài dùng chung cho Domain guard và FluentValidation validator
    public const int MinNameLength = 2;
    public const int MaxNameLength = 50;
    public const int MaxDescriptionLength = 500;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Category() { }

    public static Category Create(string name, string? description = null)
    {
        var normalizedName = NormalizeName(name);

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Slug = GenerateSlug(normalizedName),
            Description = NormalizeDescription(description),
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Cập nhật Name/Description. Slug luôn được sinh lại từ Name mới
    /// và UpdatedAt được ghi nhận. Không thay đổi Id/CreatedAt.
    /// </summary>
    public void Update(string name, string? description)
    {
        // Chuẩn hóa toàn bộ input trước khi gán để entity không bị sửa dở dang khi input sai
        var normalizedName = NormalizeName(name);
        var slug = GenerateSlug(normalizedName);
        var normalizedDescription = NormalizeDescription(description);

        Name = normalizedName;
        Slug = slug;
        Description = normalizedDescription;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Trim tên và kiểm tra độ dài trong khoảng 2..50 ký tự.</summary>
    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

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

    /// <summary>
    /// Trim mô tả, chuỗi rỗng/whitespace trở thành null, giới hạn 500 ký tự.
    /// </summary>
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

    /// <summary>
    /// Sinh URL slug không dấu từ tên category.
    /// Ví dụ: "Món Tráng Miệng" → "mon-trang-mieng", "  Đồ Ăn Vặt!!!  " → "do-an-vat".
    /// Chỉ giữ ký tự a-z và 0-9; khoảng trắng thừa và ký tự đặc biệt được gộp thành một dấu '-'.
    /// </summary>
    private static string GenerateSlug(string name)
    {
        var slugBuilder = new StringBuilder(name.Length);
        var pendingSeparator = false;

        foreach (var character in RemoveDiacritics(name.Trim().ToLowerInvariant()))
        {
            if (IsSlugCharacter(character))
            {
                if (pendingSeparator && slugBuilder.Length > 0)
                {
                    slugBuilder.Append('-');
                }

                slugBuilder.Append(character);
                pendingSeparator = false;
                continue;
            }

            pendingSeparator = true;
        }

        if (slugBuilder.Length == 0)
        {
            throw new ArgumentException(
                "Name must contain at least one letter or digit so a slug can be generated.",
                nameof(name));
        }

        return slugBuilder.ToString();
    }

    /// <summary>Bỏ dấu tiếng Việt: "Đồ Ăn" → "Do An" (đ/Đ được đổi thành d).</summary>
    private static string RemoveDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character == 'đ' ? 'd' : character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    // Slug chỉ chứa ký tự an toàn cho URL: a-z và 0-9
    private static bool IsSlugCharacter(char character)
        => (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9');
}