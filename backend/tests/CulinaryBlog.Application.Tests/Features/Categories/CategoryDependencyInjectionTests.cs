using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories;

/// <summary>
/// Kiểm tra validator của Category được đăng ký trong DI và ValidationBehavior
/// thực sự chạy validation trong pipeline MediatR (EF Core InMemory).
/// </summary>
public class CategoryDependencyInjectionTests
{
    private static ServiceProvider BuildProvider(CategoryTestDbContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IApplicationDbContext>(_ => context);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddApplication_ShouldRegisterCategoryValidators()
    {
        // Arrange
        using var context = CategoryTestDbContext.CreateInMemory();
        using var provider = BuildProvider(context);

        // Act & Assert: mỗi command/query có đúng một validator, không đăng ký trùng
        provider.GetServices<IValidator<CreateCategoryCommand>>().Should().ContainSingle();
        provider.GetServices<IValidator<GetCategoriesQuery>>().Should().ContainSingle();
    }

    [Fact]
    public async Task Send_InvalidCreateCategoryCommand_ShouldThrowValidationException()
    {
        // Arrange
        using var context = CategoryTestDbContext.CreateInMemory();
        using var provider = BuildProvider(context);
        var sender = provider.GetRequiredService<ISender>();

        // Act: tên 1 ký tự không hợp lệ
        var act = async () => await sender.Send(new CreateCategoryCommand("A", null));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();

        var storedCount = await context.Categories.CountAsync();
        storedCount.Should().Be(0);
    }

    [Fact]
    public async Task Send_ValidCreateCategoryCommand_ShouldReachHandlerAndPersistCategory()
    {
        // Arrange
        using var context = CategoryTestDbContext.CreateInMemory();
        using var provider = BuildProvider(context);
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.Send(new CreateCategoryCommand("  Món Chay  ", "  Thanh đạm  "));

        // Assert
        result.Name.Should().Be("Món Chay");
        result.Slug.Should().Be("mon-chay");
        result.Description.Should().Be("Thanh đạm");

        var storedCount = await context.Categories.CountAsync();
        storedCount.Should().Be(1);
    }
}
