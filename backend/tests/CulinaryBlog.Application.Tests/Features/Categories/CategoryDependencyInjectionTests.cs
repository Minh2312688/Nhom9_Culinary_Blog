using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Tests.Common;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories;

/// <summary>
/// Kiểm tra validator của Category được đăng ký trong DI và ValidationBehavior
/// thực sự chạy validation trong pipeline MediatR (EF Core InMemory).
/// </summary>
public class CategoryDependencyInjectionTests
{
    private static ServiceProvider BuildProvider(
        CategoryTestDbContext context,
        ListLogger<AuditLoggingBehavior<CreateCategoryCommand, CategoryDto>>? auditLogger = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        // O-04: AuditLoggingBehavior (registered in AddApplication) depends on
        // ICurrentUserService; the test DI container must register a stub so
        // MediatR can instantiate the behavior for any request.
        services.AddScoped<ICurrentUserService>(_ => Mock.Of<ICurrentUserService>());
        services.AddScoped<IApplicationDbContext>(_ => context);
        services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(context));
        services.AddScoped<ICategoryRepository>(_ => new CategoryRepository(context));
        // Category handlers phụ thuộc ICategoryCache; unit test dùng fake in-memory.
        services.AddSingleton<ICategoryCache, FakeCategoryCache>();
        if (auditLogger is not null)
        {
            services.AddSingleton<ILogger<AuditLoggingBehavior<CreateCategoryCommand, CategoryDto>>>(auditLogger);
        }

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
        provider.GetServices<IValidator<UpdateCategoryCommand>>().Should().ContainSingle();
        provider.GetServices<IValidator<GetCategoriesQuery>>().Should().ContainSingle();
        provider.GetServices<IValidator<GetCategoryBySlugQuery>>().Should().ContainSingle();
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
    public async Task Send_InvalidCreateCategoryCommand_ShouldLogAuditFailure()
    {
        using var context = CategoryTestDbContext.CreateInMemory();
        var auditLogger = new ListLogger<AuditLoggingBehavior<CreateCategoryCommand, CategoryDto>>();
        using var provider = BuildProvider(context, auditLogger);
        var sender = provider.GetRequiredService<ISender>();

        var act = async () => await sender.Send(new CreateCategoryCommand("A", "not logged"));

        await act.Should().ThrowAsync<ValidationException>();
        var auditLogs = auditLogger.Entries
            .Where(entry => entry.Message.StartsWith("Audit", StringComparison.Ordinal))
            .ToList();

        auditLogs.Should().ContainSingle();
        auditLogs[0].Message.Should().Contain("CreateCategoryCommand");
        auditLogs[0].Message.Should().Contain("Failure");
        auditLogs[0].Message.Should().Contain(nameof(ValidationException));
        auditLogs[0].Message.Should().NotContain("not logged");
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
