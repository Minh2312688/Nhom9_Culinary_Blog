using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Contracts.Persistence;
public interface IApplicationDbContext
{
 DbSet<Category> Categories { get; }
 DbSet<Recipe> Recipes { get; }
 // Lưu thay đổi — trả về số bản ghi bị ảnh hưởng
 Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
