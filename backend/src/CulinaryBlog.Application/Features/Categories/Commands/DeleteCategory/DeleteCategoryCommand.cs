using MediatR;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
// Command xóa category — chỉ cần ID, không trả về data (Unit = void trong MediatR)
public record DeleteCategoryCommand(Guid Id) : IRequest<Unit>;
// ─── Handler ─────────────────────────────────────────────────────────────────
public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
 private readonly IApplicationDbContext _context;
 private readonly ICategoryCache _cache;
 public DeleteCategoryCommandHandler(IApplicationDbContext context, ICategoryCache cache)
 {
 _context = context;
 _cache = cache;
 }
 public async Task<Unit> Handle(
 DeleteCategoryCommand request,
 CancellationToken cancellationToken)
 {
 // Query có global query filter nên category đã soft delete trả về NotFound
 var category = await _context.Categories
 .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
 ?? throw new NotFoundException(nameof(Category), request.Id);
 // Delete guard: chỉ Recipe active (không bị soft delete) mới chặn xóa Category.
 // Đây là hướng an toàn hiện tại; chưa được ghi chính thức vào SRS-CONFLICTS-AND-DECISIONS.md.
 var hasActiveRecipes = await _context.Recipes
 .AnyAsync(r => r.CategoryId == request.Id, cancellationToken);
 if (hasActiveRecipes)
 {
 throw new ConflictException(
 "Category cannot be deleted while it still has recipes.");
 }
 // Soft delete (CONFLICT-002): giữ row, IsDeleted = true, không thêm DeletedAt
 category.Delete();
 await _context.SaveChangesAsync(cancellationToken);
 await _cache.InvalidateAsync(cancellationToken);
 return Unit.Value; // Tương đương void trong MediatR
 }
}