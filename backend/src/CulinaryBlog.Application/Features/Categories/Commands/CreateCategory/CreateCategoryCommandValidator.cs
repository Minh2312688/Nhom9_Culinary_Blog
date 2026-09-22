using CulinaryBlog.Domain.Entities;
using FluentValidation;

namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Validate input CreateCategoryCommand trước khi handler chạy.
/// ValidationBehavior ném ValidationException để tầng API trả lỗi theo contract hiện có.
/// Giới hạn độ dài lấy trực tiếp từ Category để Domain và Application luôn nhất quán.
/// Khả năng sinh slug (ví dụ tên chỉ gồm ký tự đặc biệt) do Category domain kiểm tra;
/// validator không lặp lại logic slug để không từ chối sai tên tiếng Việt có dấu
/// như "Đồ", "Ăn", "Ức" (các tên này không có ký tự ASCII nhưng vẫn sinh được slug).
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
                .WithMessage($"Name must not exceed {Category.MaxNameLength} characters.");

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
}
