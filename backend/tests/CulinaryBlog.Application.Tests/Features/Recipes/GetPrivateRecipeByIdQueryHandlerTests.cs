using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class GetPrivateRecipeByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_AdminCanReadDraftById()
    {
        var recipe = new Recipe
        {
            Title = "Draft recipe",
            Slug = "draft-recipe",
            AuthorId = "author",
            CategoryId = Guid.NewGuid(),
            Status = RecipeStatus.Draft
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PrivateRecipe_{Guid.NewGuid():N}").Options);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);
        var handler = new GetPrivateRecipeByIdQueryHandler(
            Mock.Of<CulinaryBlog.Application.Contracts.Persistence.IRecipeRepository>(repository =>
                repository.Query == db.Recipes),
            currentUser.Object);

        var result = await handler.Handle(
            new GetPrivateRecipeByIdQuery(recipe.Id), CancellationToken.None);

        Assert.Equal(recipe.Id, result.Id);
    }
}
