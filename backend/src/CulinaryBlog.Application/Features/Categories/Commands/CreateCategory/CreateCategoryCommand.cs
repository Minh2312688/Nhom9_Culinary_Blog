using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
namespace CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
public class CreateCategoryCommandHandler
 : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
 private readonly IApplicationDbContext _context;
 // Constructor Injection — DI container tự động cung cấp IApplicationDbContext
 public CreateCategoryCommandHandler(IApplicationDbContext context)
 {
 _context = context;
 }
 public async Task<CategoryDto> Handle(
 CreateCategoryCommand request,
 CancellationToken cancellationToken)
 {
 // 1. Tạo entity qua Domain factory method — đảm bảo business rules
 var category = Category.Create(request.Name, request.Description);
 // 2. Thêm vào DbContext (chưa ghi vào database)
 _context.Categories.Add(category);
 // 3. Ghi vào database (thực thi SQL INSERT)
 await _context.SaveChangesAsync(cancellationToken);
 // 4. Map sang DTO để trả về — Presentation Layer không nhận raw Entity
 return category.Adapt<CategoryDto>();
 }
}

public sealed record CreateCategoryCommand(
    string Name,
    string? Description
) : IRequest<CategoryDto>;
