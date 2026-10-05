using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class CategoryRepository(IApplicationDbContext context) : ICategoryRepository
{
    public IQueryable<Category> Query => context.Categories;
    public void Add(Category category) => context.Categories.Add(category);
    public void Remove(Category category) => context.Categories.Remove(category);
}
