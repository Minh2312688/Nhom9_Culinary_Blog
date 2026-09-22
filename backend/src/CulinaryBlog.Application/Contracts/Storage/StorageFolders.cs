namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Folder đích trong bucket culinary-blog (SRS §12.2): recipes/{recipeId} và categories.
/// </summary>
public static class StorageFolders
{
    public const string Categories = "categories";

    public const int MaxLength = 200;

    private const string AllowedSegmentCharacters = "abcdefghijklmnopqrstuvwxyz0123456789-";

    public static string ForRecipe(Guid recipeId)
    {
        if (recipeId == Guid.Empty)
        {
            throw new ArgumentException("Recipe id must not be empty.", nameof(recipeId));
        }

        return $"recipes/{recipeId:N}";
    }

    /// <summary>
    /// Folder hợp lệ khi mọi segment chỉ gồm a-z, 0-9 và ký tự gạch ngang, ngăn cách bởi dấu /.
    /// Nhờ đó tự động chặn path traversal, backslash, chữ hoa và dấu / ở đầu hoặc cuối.
    /// </summary>
    public static bool IsValid(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        var trimmedFolder = folder.Trim();

        if (trimmedFolder.Length > MaxLength)
        {
            return false;
        }

        foreach (var segment in trimmedFolder.Split("/"))
        {
            if (!IsValidSegment(segment))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSegment(string segment)
    {
        if (segment.Length == 0)
        {
            return false;
        }

        foreach (var character in segment)
        {
            if (!AllowedSegmentCharacters.Contains(character))
            {
                return false;
            }
        }

        return true;
    }
}
