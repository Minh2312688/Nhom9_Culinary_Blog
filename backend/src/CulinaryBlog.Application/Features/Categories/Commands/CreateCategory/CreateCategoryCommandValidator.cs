using CulinaryBlog.Application.Common.Validation;
using CulinaryBlog.Domain.Entities;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Validate input CreateCategoryCommand trước khi handler chạy.
/// ValidationBehavior ném ValidationException để tầng API trả lỗi theo contract hiện có.
/// Giới hạn độ dài lấy trực tiếp từ Category để Domain và Application luôn nhất quán.
/// Tên không sinh được slug (ví dụ "!!!") bị chặn ở đây để API trả 400 Problem Details
/// thay vì để Category.Create ném ArgumentException rồi thành 500.
/// Validator không tự viết lại bộ slugify: nó hỏi lại Domain qua Category.CanCreateSlug,
/// nên tên tiếng Việt có dấu như "Đồ", "Ăn", "Ức", "Ớt", "Ổi" vẫn được chấp nhận.
/// Tên chứa HTML markup (&lt;b&gt;, &lt;script&gt;, &lt;img src=x&gt;) bị chặn bằng
/// HtmlMarkup.Contains dùng chung với UpdateCategoryCommandValidator; ký tự so sánh
/// trong text thuần ("Món &lt; 30 phút") vẫn hợp lệ.
/// </summary>
public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
                .WithMessage("Name is required.")
            .Must(name => HasMinimumLength(name))
                .WithMessage($"Name must be at least {Category.MinNameLength} characters long.")
            .Must(name => HasMaximumLength(name))
                .WithMessage($"Name must not exceed {Category.MaxNameLength} characters.")
            .Must(name => !HtmlMarkup.Contains(name))
                .WithMessage("Name must not contain HTML markup.")
            .Must(name => HasGeneratableSlug(name))
                .WithMessage("Name must contain at least one letter or digit so a slug can be generated.");

        RuleFor(command => command.Description)
            .Must(description => HasValidLength(description))
                .WithMessage($"Description must not exceed {Category.MaxDescriptionLength} characters.");
    }

    // Độ dài được kiểm tra sau khi Trim để khớp với xử lý trong Category domain
    private static bool HasMinimumLength(string? name)
        => name is not null && name.Trim().Length >= Category.MinNameLength;

    private static bool HasMaximumLength(string? name)
        => name is null || name.Trim().Length <= Category.MaxNameLength;

    private static bool HasValidLength(string? description)
        => description is null || description.Trim().Length <= Category.MaxDescriptionLength;

    // Quy tắc slug vẫn thuộc Domain: helper này chỉ hỏi lại Category.CanCreateSlug.
    // Tên rỗng/whitespace đã do rule NotEmpty xử lý nên chỉ kiểm tra tên không rỗng,
    // nhờ vậy mỗi lỗi chỉ có một thông báo.
    private static bool HasGeneratableSlug(string? name)
        => string.IsNullOrWhiteSpace(name) || Category.CanCreateSlug(name);
}
