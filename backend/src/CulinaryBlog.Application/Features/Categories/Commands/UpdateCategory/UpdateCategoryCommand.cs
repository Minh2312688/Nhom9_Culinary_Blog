using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// PUT /api/v1/categories/{id} (CONFLICT-017): cập nhật Name, Description, ImageUrl, OrderIndex.
/// Slug được sinh ở Create và giữ nguyên vĩnh viễn để URL cũ không bị hỏng.
/// </summary>
public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int OrderIndex) : IRequest<CategoryDto>;

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty()
                .WithMessage("Id is required.");

        RuleFor(command => command.Name)
            .NotEmpty()
                .WithMessage("Name is required.")
            .Must(name => name is not null && name.Trim().Length >= Category.MinNameLength)
                .WithMessage($"Name must be at least {Category.MinNameLength} characters long.")
            .Must(name => name is null || name.Trim().Length <= Category.MaxNameLength)
                .WithMessage($"Name must not exceed {Category.MaxNameLength} characters.");

        RuleFor(command => command.Description)
            .Must(description => description is null ||
                description.Trim().Length <= Category.MaxDescriptionLength)
                .WithMessage($"Description must not exceed {Category.MaxDescriptionLength} characters.");

        RuleFor(command => command.ImageUrl)
            .Must(imageUrl => imageUrl is null ||
                imageUrl.Trim().Length <= Category.MaxImageUrlLength)
                .WithMessage($"ImageUrl must not exceed {Category.MaxImageUrlLength} characters.");

        RuleFor(command => command.OrderIndex)
            .GreaterThanOrEqualTo(0)
                .WithMessage("OrderIndex must be greater than or equal to 0.");
    }
}

public sealed class UpdateCategoryCommandHandler
    : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext context;
    private readonly ICategoryCache cache;

    public UpdateCategoryCommandHandler(IApplicationDbContext context, ICategoryCache cache)
    {
        this.context = context;
        this.cache = cache;
    }

    public async Task<CategoryDto> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        // Global query filter loại bỏ category đã soft delete
        var category = await context.Categories
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.Id);

        // Vì slug cố định, tên trùng phải được kiểm tra ở tầng application
        // (so sánh sau trim, không phân biệt hoa thường, trừ chính category đang sửa).
        var normalizedName = request.Name.Trim();
        var duplicateExists = await context.Categories.AnyAsync(
            c => c.Id != request.Id && c.Name.ToLower() == normalizedName.ToLower(),
            cancellationToken);
        if (duplicateExists)
        {
            throw new ConflictException($"A category named \"{normalizedName}\" already exists.");
        }

        // Domain chuẩn hoá và gán tất cả field sau khi đã kiểm tra đầy đủ
        category.Update(request.Name, request.Description, request.ImageUrl, request.OrderIndex);
        await context.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cancellationToken);

        return category.Adapt<CategoryDto>();
    }
}