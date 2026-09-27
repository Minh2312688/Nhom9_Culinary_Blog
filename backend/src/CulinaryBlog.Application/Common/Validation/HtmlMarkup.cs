using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Common.Validation;

/// <summary>
/// Nhận diện HTML markup trong text do client gửi lên, dùng chung cho các validator
/// (ví dụ Name của CreateCategoryCommand và UpdateCategoryCommand).
///
/// Chỉ khớp hình dạng thẻ hợp lệ theo HTML: thẻ mở <c>&lt;b&gt;</c>, thẻ đóng <c>&lt;/b&gt;</c>,
/// thẻ tự đóng <c>&lt;br/&gt;</c>, thẻ có thuộc tính <c>&lt;img src=x&gt;</c>,
/// comment <c>&lt;!-- ... --&gt;</c> và khai báo <c>&lt;!DOCTYPE ...&gt;</c>.
/// Tên thẻ phải bắt đầu ngay sau '&lt;' bằng một chữ cái ASCII nên text thuần có ký tự so sánh
/// như "Món &lt; 30 phút" hay "Bún &lt;3" không bị coi là markup.
/// </summary>
public static class HtmlMarkup
{
    // Thẻ mở/self-closing/đóng: <b>, <br/>, <br />, <img src=x>, <svg/onload=alert(1)>, </b>
    // Comment: <!-- ... -->
    // Khai báo: <!DOCTYPE html>
    // Tên thẻ phải là một chữ cái ASCII ngay sau '<' (hoặc '</') nên "Món < 30 phút",
    // "Bún <3" không khớp; [^<>]* chỉ cho phép nội dung bên trong một cặp ngoặc nhọn
    // nên pattern không gộp nhiều thẻ thành một khối và không thể backtracking bùng nổ.
    private static readonly Regex MarkupPattern = new(
        pattern: @"<(?:(?:/?[A-Za-z][A-Za-z0-9-]*[^<>]*)|(?:!--[^<>]*?--)|(?:![A-Za-z][^<>]*))>",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Trả <c>true</c> khi <paramref name="value"/> chứa HTML markup.
    /// Null/rỗng và khoảng trắng thuần trả <c>false</c> để rule bắt buộc (NotEmpty)
    /// vẫn là rule duy nhất báo lỗi cho input thiếu.
    /// </summary>
    public static bool Contains(string? value)
        => !string.IsNullOrWhiteSpace(value) && MarkupPattern.IsMatch(value);
}
