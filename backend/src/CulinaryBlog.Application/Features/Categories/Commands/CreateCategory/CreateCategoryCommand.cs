using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
public class CreateCategoryCommandHandler
 : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
 private readonly IApplicationDbContext _context;
 private readonly ICategoryCache _cache;
 // Constructor Injection — DI container tự động cung cấp IApplicationDbContext
 public CreateCategoryCommandHandler(IApplicationDbContext context, ICategoryCache cache)
 {
 _context = context;
 _cache = cache;
 }
 public async Task<CategoryDto> Handle(
 CreateCategoryCommand request,
 CancellationToken cancellationToken)
 {
 // 1. Tạo entity qua Domain factory method — đảm bảo business rules
 var category = Category.Create(request.Name, request.Description);
 // 2. Chặn trùng slug: IX_Categories_Slug là unique và vẫn giữ row đã soft delete
 var slugTaken = await _context.Categories
 .IgnoreQueryFilters()
 .AnyAsync(c => c.Slug == category.Slug, cancellationToken);
 if (slugTaken)
 {
 throw new ConflictException($"A category with slug \"{category.Slug}\" already exists.");
 }
 // 3. Thêm vào DbContext (chưa ghi vào database)
 _context.Categories.Add(category);
 // 4. Ghi vào database (thực thi SQL INSERT)
 await _context.SaveChangesAsync(cancellationToken);
 // 5. Invalidate cache để list/detail không còn dữ liệu cũ
 await _cache.InvalidateAsync(cancellationToken);
 // 6. Map sang DTO để trả về — Presentation Layer không nhận raw Entity
 return category.Adapt<CategoryDto>();
 }
}

public sealed record CreateCategoryCommand(
    string Name,
    string? Description
) : IRequest<CategoryDto>;
