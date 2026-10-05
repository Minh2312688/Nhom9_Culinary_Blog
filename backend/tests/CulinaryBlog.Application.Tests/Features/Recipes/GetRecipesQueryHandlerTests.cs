using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Entities;
using Moq;
using Xunit;
using System.Collections.Concurrent;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class GetRecipesQueryHandlerTests
{
    [Fact]
    public async Task Search_RemovesTsQuerySyntaxAndKeepsUnicodeTokens()
    {
        var expected = new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), 0, 1, 12);
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.SearchAsync(
                "phở tôm", null, false, 1, 12, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new GetRecipesQueryHandler(recipes.Object, new Mock<ICurrentUserService>().Object);

        var result = await handler.Handle(
            new GetRecipesQuery(Search: "\"Phở\"  tôm:* | !!"), CancellationToken.None);

        Assert.Same(expected, result);
        recipes.VerifyAll();
    }

    [Fact]
    public async Task UnfilteredSearch_ShouldQueryRepositoryUntilPopularityPolicyIsDefined()
    {
        var expected = new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), 0, 1, 12);
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.SearchAsync(
                "ramen", null, false, 1, 12, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new GetRecipesQueryHandler(recipes.Object, new Mock<ICurrentUserService>().Object);

        var result = await handler.Handle(new GetRecipesQuery(Search: "Ramen"), CancellationToken.None);

        Assert.Same(expected, result);
        recipes.VerifyAll();
    }

    [Fact]
    public async Task SearchWithFilters_ShouldPassAllFiltersAndBypassCache()
    {
        var categoryId = Guid.NewGuid();
        var expected = new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), 0, 2, 24);
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.SearchAsync(
                "phở", null, false, 2, 24, categoryId, RecipeDifficulty.Medium, 30, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var currentUser = new Mock<ICurrentUserService>();
        var handler = new GetRecipesQueryHandler(recipes.Object, currentUser.Object);
        var query = new GetRecipesQuery(
            Page: 2,
            PageSize: 24,
            CategoryId: categoryId,
            Difficulty: "medium",
            MaxCookTime: 30,
            MinServings: 4,
            Search: "Phở");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Same(expected, result);
        recipes.VerifyAll();
    }

    [Fact]
    public async Task PopularSearch_ShouldCacheOnlyAfterThirdObservation()
    {
        var expected = new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), 0, 1, 12);
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.SearchAsync(
                "ramen", null, false, 1, 12, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var cache = new RecordingRecipeCache();
        var handler = new GetRecipesQueryHandler(recipes.Object, new Mock<ICurrentUserService>().Object, cache);

        await handler.Handle(new GetRecipesQuery(Search: "ramen"), CancellationToken.None);
        await handler.Handle(new GetRecipesQuery(Search: "ramen"), CancellationToken.None);
        await handler.Handle(new GetRecipesQuery(Search: "ramen"), CancellationToken.None);
        await handler.Handle(new GetRecipesQuery(Search: "ramen"), CancellationToken.None);

        Assert.Equal(3, recipes.Invocations.Count(invocation => invocation.Method.Name == nameof(IRecipeRepository.SearchAsync)));
        Assert.Single(cache.Values.Values.OfType<PaginatedResult<RecipeSummaryDto>>());
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public ConcurrentDictionary<string, object> Values { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.TryGetValue(key, out var value) ? (T?)value : default);

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            Values[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
