using CulinaryBlog.Application.Abstractions.Search;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

using Xunit.Abstractions;

namespace CulinaryBlog.Integration.Tests.Search;

/// <summary>
/// Integration tests verifying PostgreSQL Full-Text Search and GIN index
/// for Lab 03 (Personal Scope: Nguyen Pham Phu Nam - MSSV 2312695).
/// Executes queries directly against the running PostgreSQL database.
/// </summary>
public class PostgresRecipeSearchRepositoryTests : IDisposable
{
    private readonly AuthDbContext _context;
    private readonly PostgresRecipeSearchRepository _repository;
    private readonly ITestOutputHelper _output;

    private const string DefaultPostgresConnection =
        "Host=localhost;Port=5432;Database=culinary_blog;Username=culinary;Password=change_this_postgres_password";

    public PostgresRecipeSearchRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
        var connectionString = Environment.GetEnvironmentVariable("TEST_REAL_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("Username=test;"))
        {
            connectionString = DefaultPostgresConnection;
        }

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _context = new AuthDbContext(options);
        _repository = new PostgresRecipeSearchRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void DatabaseProvider_ShouldBe_PostgreSql()
    {
        // Act & Assert
        _context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL",
            "Full-Text Search queries must execute against real PostgreSQL, not InMemory database.");
    }

    [Fact]
    public async Task SearchAsync_WithKnownTerm_Pho_ShouldReturnMatchingRecipes()
    {
        // Act
        var results = await _repository.SearchAsync("Phở", skip: 0, take: 10);

        // Assert
        results.Should().NotBeEmpty("Lab 02 seed data contains recipes with 'Phở bò gia truyền'");
        results.Should().AllSatisfy(r =>
        {
            var matchTitle = r.Title.Contains("Phở", StringComparison.OrdinalIgnoreCase);
            var matchDesc = r.Description != null && r.Description.Contains("Phở", StringComparison.OrdinalIgnoreCase);
            (matchTitle || matchDesc).Should().BeTrue("Every returned recipe must match the search term in Title or Description");
        });
    }

    [Fact]
    public async Task SearchAsync_WithKnownTerm_Bo_ShouldReturnMatchingRecipes()
    {
        // Act
        var results = await _repository.SearchAsync("bò", skip: 0, take: 10);

        // Assert
        results.Should().NotBeEmpty("Lab 02 seed data contains recipes with 'bò'");
        results.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_WithNonsenseTerm_ShouldReturnEmptyList()
    {
        // Act
        var results = await _repository.SearchAsync("xyznonexistentterm12345");

        // Assert
        results.Should().BeEmpty("Nonexistent search terms must return 0 results");
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceOrEmpty_ShouldReturnEmptyList()
    {
        // Act
        var resultsEmpty = await _repository.SearchAsync("");
        var resultsWhitespace = await _repository.SearchAsync("   ");

        // Assert
        resultsEmpty.Should().BeEmpty();
        resultsWhitespace.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Pagination_SkipAndTake_ShouldWorkCorrectly()
    {
        // Act
        var page1 = await _repository.SearchAsync("bò", skip: 0, take: 2);
        var page2 = await _repository.SearchAsync("bò", skip: 2, take: 2);

        // Assert
        if (page1.Count == 2 && page2.Count > 0)
        {
            var page1Ids = page1.Select(r => r.Id).ToList();
            var page2Ids = page2.Select(r => r.Id).ToList();

            page1Ids.Intersect(page2Ids).Should().BeEmpty("Pagination pages must contain distinct recipes");
        }
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnRequiredDtoFields()
    {
        // Act
        var results = await _repository.SearchAsync("Phở", skip: 0, take: 1);

        // Assert
        results.Should().NotBeEmpty();
        var item = results[0];
        item.Id.Should().NotBeEmpty();
        item.Title.Should().NotBeNullOrWhiteSpace();
        item.Slug.Should().NotBeNullOrWhiteSpace();
        item.CategoryId.Should().NotBeEmpty();
    }

    [Fact]
    public void SearchQuery_GeneratedSql_ShouldMatch_GinIndexExpression()
    {
        // Act
        var query = _context.Recipes
            .AsNoTracking()
            .Where(r => EF.Functions.ToTsVector("simple", (r.Title ?? "") + " " + (r.Description ?? ""))
                .Matches(EF.Functions.PlainToTsQuery("simple", "Phở")))
            .OrderBy(r => r.Title)
            .Skip(0)
            .Take(20)
            .Select(r => new RecipeSearchResult(
                r.Id,
                r.Title,
                r.Slug,
                r.Description,
                r.CategoryId,
                r.PrepTimeMinutes,
                r.CookTimeMinutes,
                r.Servings,
                r.Difficulty
            ));

        var sql = query.ToQueryString();
        _output.WriteLine("=== Generated SQL for SearchQuery ===");
        _output.WriteLine(sql);

        // Assert: verify exact conceptual FTS expression shape
        sql.Should().Contain("to_tsvector('simple'");
        sql.Should().Contain("plainto_tsquery('simple'");
        sql.Should().Contain("@@");

        // Verify Title is used directly (without COALESCE) and Description uses COALESCE
        sql.Should().MatchRegex(@"(?i)""Title""\s*\|\|\s*' '\s*\|\|\s*COALESCE\(.*""Description"",\s*''\)");
        sql.Should().NotMatchRegex(@"(?i)COALESCE\(.*""Title""");
    }
}
