using CulinaryBlog.Domain;
using NetArchTest.Rules;
using Xunit;

namespace CulinaryBlog.ArchitectureTests;

public sealed class CleanArchitectureTests
{
    private const string DomainNamespace = "CulinaryBlog.Domain";
    private const string ApplicationNamespace = "CulinaryBlog.Application";
    private const string InfrastructureNamespace = "CulinaryBlog.Infrastructure";
    private const string ApiNamespace = "CulinaryBlog.API";

    [Fact]
    public void Domain_Should_Not_HaveDependencyOn_OtherProjects()
    {
        // Arrange
        var assembly = typeof(AssemblyMarker).Assembly;

        var otherProjects = new[]
        {
            ApplicationNamespace,
            InfrastructureNamespace,
            ApiNamespace
        };

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        // Assert
        Assert.True(testResult.IsSuccessful, "Domain must not have dependencies on Application, Infrastructure, or API.");
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_InfrastructureOrApi()
    {
        // Arrange
        var assembly = typeof(CulinaryBlog.Application.DependencyInjection).Assembly;

        var forbiddenProjects = new[]
        {
            InfrastructureNamespace,
            ApiNamespace
        };

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenProjects)
            .GetResult();

        // Assert
        Assert.True(testResult.IsSuccessful, "Application must not have dependencies on Infrastructure or API.");
    }

    [Fact]
    public void Infrastructure_Should_Not_HaveDependencyOn_Api()
    {
        // Arrange
        var assembly = typeof(CulinaryBlog.Infrastructure.DependencyInjection).Assembly;

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        // Assert
        Assert.True(testResult.IsSuccessful, "Infrastructure must not have dependencies on API.");
    }
}
