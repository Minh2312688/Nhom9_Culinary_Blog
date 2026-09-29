using MediatR;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Common.Exceptions;
namespace CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
// Command xóa category — chỉ cần ID, không trả về data (Unit = void trong MediatR)
public record DeleteCategoryCommand(Guid Id) : IRequest<Unit>;
// ─── Handler ─────────────────────────────────────────────────────────────────
public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
 private readonly IApplicationDbContext _context;
 public DeleteCategoryCommandHandler(IApplicationDbContext context)
 => _context = context;
 public async Task<Unit> Handle(
 DeleteCategoryCommand request,
 CancellationToken cancellationToken)
 {
 // Tìm entity — ném NotFoundException nếu không tồn tại
 // (NotFoundException sẽ được catch ở Global Exception Handler — Chương 5)
 var category = await _context.Categories
 .FindAsync([request.Id], cancellationToken)
 ?? throw new NotFoundException(nameof(Category), request.Id);
 _context.Categories.Remove(category);
 await _context.SaveChangesAsync(cancellationToken);
 return Unit.Value; // Tương đương void trong MediatR
 }
}