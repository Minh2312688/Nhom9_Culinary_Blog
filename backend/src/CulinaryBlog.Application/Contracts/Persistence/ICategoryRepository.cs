using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Contracts.Persistence;

public interface ICategoryRepository
{
    IQueryable<Category> Query { get; }
    void Add(Category category);
    void Remove(Category category);
}
