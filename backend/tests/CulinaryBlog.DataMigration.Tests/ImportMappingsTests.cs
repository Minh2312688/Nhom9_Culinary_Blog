using CulinaryBlog.DataMigration;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.DataMigration.Tests;

public class ImportMappingsTests
{
    [Theory]
    [InlineData("Easy", 1)]
    [InlineData("Medium", 2)]
    [InlineData("Hard", 3)]
    [InlineData("Expert", 4)]
    [InlineData(" easy ", 1)]
    public void ParseDifficulty_maps_known_names(string value, int expected)
    {
        ImportMappings.ParseDifficulty(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Impossible")]
    public void ParseDifficulty_rejects_unknown_names(string? value)
    {
        var action = () => ImportMappings.ParseDifficulty(value);

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Author", "Author")]
    [InlineData("author", "Author")]
    [InlineData("Admin", "Admin")]
    [InlineData(" admin ", "Admin")]
    public void NormalizeRole_returns_supported_canonical_role(string value, string expected)
    {
        ImportMappings.NormalizeRole(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SuperAdmin")]
    public void NormalizeRole_rejects_unknown_roles(string? value)
    {
        var action = () => ImportMappings.NormalizeRole(value);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnsureSeparateDatabases_rejects_matching_database_names()
    {
        var action = () => ImportMappings.EnsureSeparateDatabases(
            "Host=localhost;Database=culinary_blog;Username=postgres",
            "Host=localhost;Database=culinary_blog;Username=postgres");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnsureSeparateDatabases_allows_distinct_database_names()
    {
        var action = () => ImportMappings.EnsureSeparateDatabases(
            "Host=localhost;Database=culinary_blog;Username=postgres",
            "Host=localhost;Database=culinary_blog_auth;Username=postgres");

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no")]
    public void EnsureBackupVerified_requires_explicit_confirmation(string? value)
    {
        var action = () => ImportMappings.EnsureBackupVerified(value);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnsureBackupVerified_accepts_yes()
    {
        var action = () => ImportMappings.EnsureBackupVerified("yes");

        action.Should().NotThrow();
    }

    [Fact]
    public void GetCategoryDefaults_initializes_fields_missing_from_source()
    {
        ImportMappings.GetCategoryDefaults().Should().Be(new CategoryImportDefaults(null, 0, false));
    }

    [Fact]
    public void AreEquivalent_compares_byte_arrays_by_content()
    {
        ImportMappings.AreEquivalent(new byte[] { 0, 1, 2 }, new byte[] { 0, 1, 2 }).Should().BeTrue();
        ImportMappings.AreEquivalent(new byte[] { 0, 1 }, new byte[] { 0, 2 }).Should().BeFalse();
    }

    [Fact]
    public void AreEquivalent_compares_timestamp_instants_in_utc()
    {
        var utc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var offset = new DateTimeOffset(2026, 9, 28, 14, 0, 0, TimeSpan.FromHours(2));

        ImportMappings.AreEquivalent(utc, offset).Should().BeTrue();
    }
}
