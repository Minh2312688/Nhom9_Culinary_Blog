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
 private readonly ICategoryRepository _categories;
 private readonly IUnitOfWork _unitOfWork;
 private readonly ICategoryCache _cache;
 public CreateCategoryCommandHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, ICategoryCache cache)
 {
 _categories = categories;
 _unitOfWork = unitOfWork;
 _cache = cache;
 }
 public async Task<CategoryDto> Handle(
 CreateCategoryCommand request,
 CancellationToken cancellationToken)
 {
 // 1. Tạo entity qua Domain factory method — đảm bảo business rules
 var category = Category.Create(request.Name, request.Description);
 if (await _categories.Query.IgnoreQueryFilters().AnyAsync(existing => existing.Slug == category.Slug, cancellationToken))
     throw new ConflictException($"A category with slug '{category.Slug}' already exists.");
 // 2. Thêm vào DbContext (chưa ghi vào database)
 _categories.Add(category);
 // 3. Ghi vào database (thực thi SQL INSERT)
 await _unitOfWork.SaveChangesAsync(cancellationToken);
 await _cache.InvalidateAsync(cancellationToken);
 // 4. Map sang DTO để trả về — Presentation Layer không nhận raw Entity
 return category.Adapt<CategoryDto>();
 }
}

public sealed record CreateCategoryCommand(
    string Name,
    string? Description
) : IRequest<CategoryDto>;
