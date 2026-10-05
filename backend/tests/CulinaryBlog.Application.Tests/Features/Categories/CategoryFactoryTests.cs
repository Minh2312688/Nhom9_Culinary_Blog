using CulinaryBlog.Domain.Entities;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories;

public sealed class CategoryFactoryTests
{
    [Fact]
    public void Create_ShouldGenerateSlugFromVietnameseName()
    {
        var category = Category.Create("Món ăn Việt Nam", "Món truyền thống");

        Assert.Equal("mon-an-viet-nam", category.Slug);
        Assert.Equal("Món ăn Việt Nam", category.Name);
        Assert.Equal("Món truyền thống", category.Description);
    }
}
