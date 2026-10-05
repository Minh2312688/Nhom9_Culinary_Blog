using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
// Command xóa category — chỉ cần ID, không trả về data (Unit = void trong MediatR)
public record DeleteCategoryCommand(Guid Id) : IRequest<Unit>;
// ─── Handler ─────────────────────────────────────────────────────────────────
public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
 private readonly IApplicationDbContext _context;
 private readonly CulinaryBlog.Application.Contracts.ICategoryCache _cache;
 public DeleteCategoryCommandHandler(IApplicationDbContext context, CulinaryBlog.Application.Contracts.ICategoryCache cache)
 { _context = context; _cache = cache; }
 public async Task<Unit> Handle(
 DeleteCategoryCommand request,
 CancellationToken cancellationToken)
 {
 // Tìm entity — ném NotFoundException nếu không tồn tại
 // (NotFoundException sẽ được catch ở Global Exception Handler — Chương 5)
 var category = await _context.Categories
 .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
 ?? throw new NotFoundException(nameof(Category), request.Id);
 if (await _context.Recipes.AnyAsync(recipe => recipe.CategoryId == category.Id, cancellationToken))
     throw new ConflictException("A category with active recipes cannot be deleted.");
 category.Delete();
 await _context.SaveChangesAsync(cancellationToken);
 await _cache.InvalidateAsync(cancellationToken);
 return Unit.Value; // Tương đương void trong MediatR
 }
}
