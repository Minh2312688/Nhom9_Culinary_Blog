using System.Reflection;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Users;
using CulinaryBlog.Application.Features.Users.GetUsers;
using CulinaryBlog.Domain.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Users;

public class GetUsersTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly GetUsersQueryHandler _handler;
    private readonly GetUsersQueryValidator _validator = new();

    public GetUsersTests()
    {
        _handler = new GetUsersQueryHandler(_identityServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidPagination_ReturnsPagedUsers_WithoutSensitiveData()
    {
        // Arrange
        var users = new List<UserListItemDto>
        {
            new(
                Id: "user-1",
                Email: "admin@culinary.local",
                DisplayName: "Administrator",
                AvatarUrl: null,
                Bio: "System Admin",
                EmailConfirmed: true,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-30),
                IsActive: true,
                IsLockedOut: false,
                LockoutEnd: null,
                Roles: new List<string> { AppRoles.Admin }
            ),
            new(
                Id: "user-2",
                Email: "author@culinary.local",
                DisplayName: "Gordon Ramsay",
                AvatarUrl: "https://example.com/avatar.jpg",
                Bio: "Head Chef",
                EmailConfirmed: true,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-10),
                IsActive: true,
                IsLockedOut: false,
                LockoutEnd: null,
                Roles: new List<string> { AppRoles.Author }
            )
        };

        var paginatedResult = new PaginatedResult<UserListItemDto>(users, totalCount: 2, page: 1, pageSize: 10);

        _identityServiceMock
            .Setup(x => x.GetUsersAsync(1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var query = new GetUsersQuery(Page: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(1);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
        result.Items.Should().HaveCount(2);

        var adminUser = result.Items[0];
        adminUser.Id.Should().Be("user-1");
        adminUser.Email.Should().Be("admin@culinary.local");
        adminUser.DisplayName.Should().Be("Administrator");
        adminUser.Roles.Should().Contain(AppRoles.Admin);

        var authorUser = result.Items[1];
        authorUser.Id.Should().Be("user-2");
        authorUser.Email.Should().Be("author@culinary.local");
        authorUser.DisplayName.Should().Be("Gordon Ramsay");
        authorUser.Roles.Should().Contain(AppRoles.Author);
    }

    [Fact]
    public async Task Handle_WithRoleFilter_PassesRoleToIdentityService()
    {
        // Arrange
        var paged = new PaginatedResult<UserListItemDto>(new List<UserListItemDto>(), 0, 1, 10);
        _identityServiceMock
            .Setup(x => x.GetUsersAsync(1, 10, AppRoles.Author, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var query = new GetUsersQuery(Page: 1, PageSize: 10, Role: AppRoles.Author);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _identityServiceMock.Verify(x => x.GetUsersAsync(1, 10, AppRoles.Author, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_PassesSearchTermToIdentityService()
    {
        // Arrange
        var paged = new PaginatedResult<UserListItemDto>(new List<UserListItemDto>(), 0, 1, 10);
        _identityServiceMock
            .Setup(x => x.GetUsersAsync(1, 10, null, "gordon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var query = new GetUsersQuery(Page: 1, PageSize: 10, SearchTerm: "gordon");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _identityServiceMock.Verify(x => x.GetUsersAsync(1, 10, null, "gordon", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_ValidQuery_PassesValidation()
    {
        var query = new GetUsersQuery(Page: 1, PageSize: 10, Role: "Author", SearchTerm: "chef");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_PageLessThan1_FailsValidation(int invalidPage)
    {
        var query = new GetUsersQuery(Page: invalidPage, PageSize: 10);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page)
            .WithErrorMessage("Page must be at least 1.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(51)]
    [InlineData(100)]
    public void Validator_PageSizeOutOfRange_FailsValidation(int invalidPageSize)
    {
        var query = new GetUsersQuery(Page: 1, PageSize: invalidPageSize);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage("PageSize must be between 1 and 50.");
    }

    [Fact]
    public void Validator_SearchTermExceedsLimit_FailsValidation()
    {
        var query = new GetUsersQuery(Page: 1, PageSize: 10, SearchTerm: new string('x', 101));
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.SearchTerm)
            .WithErrorMessage("SearchTerm must not exceed 100 characters.");
    }

    [Fact]
    public void Security_UserListItemDto_DoesNotExposeSensitiveFields()
    {
        // Audit that sensitive fields are strictly absent from UserListItemDto
        var properties = typeof(UserListItemDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        properties.Should().NotContain("passwordhash");
        properties.Should().NotContain("securitystamp");
        properties.Should().NotContain("concurrencystamp");
        properties.Should().NotContain("refreshtoken");
        properties.Should().NotContain("resettoken");
        properties.Should().NotContain("token");
    }
}

