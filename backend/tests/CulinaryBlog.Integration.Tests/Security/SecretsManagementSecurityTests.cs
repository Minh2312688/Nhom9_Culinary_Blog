using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

/// <summary>
/// NFR-SEC-007 Integration & System Security Tests:
/// Verifies that no production credentials or raw secrets exist in tracked configuration files,
/// .gitignore excludes sensitive files (.env, *.user), and appsettings does not contain real private keys.
/// </summary>
public class SecretsManagementSecurityTests
{
    private readonly string _repoRoot;

    public SecretsManagementSecurityTests()
    {
        // Navigate up to solution / repository root
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CulinaryBlog.sln")) &&
               !File.Exists(Path.Combine(dir.FullName, "backend", "CulinaryBlog.sln")))
        {
            dir = dir.Parent;
        }

        _repoRoot = dir?.FullName ?? currentDir;
        if (File.Exists(Path.Combine(_repoRoot, "backend", "CulinaryBlog.sln")))
        {
            // Already at root
        }
        else if (dir?.Parent != null && File.Exists(Path.Combine(dir.Parent.FullName, "backend", "CulinaryBlog.sln")))
        {
            _repoRoot = dir.Parent.FullName;
        }
    }

    [Fact]
    public void GitIgnore_ShouldExcludeDotEnvAndUserFiles()
    {
        var gitIgnorePath = Path.Combine(_repoRoot, ".gitignore");
        if (File.Exists(gitIgnorePath))
        {
            var content = File.ReadAllText(gitIgnorePath);
            content.Should().Contain(".env");
            content.Should().Contain("*.user");
        }
    }

    [Fact]
    public void AppsettingsJson_ShouldNotContainRealJwtSecret()
    {
        var appsettingsPath = Path.Combine(_repoRoot, "backend", "src", "CulinaryBlog.API", "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            var content = File.ReadAllText(appsettingsPath);
            // Must NOT have Jwt:Key configured with a real secret in source control
            content.Should().NotContain("Jwt__Key");
            content.Should().NotMatchRegex(@"""Key""\s*:\s*""(?!change_me|replace_with)[a-zA-Z0-9_\-!]{32,}""");
        }
    }

    [Fact]
    public void AppsettingsDevelopmentJson_ShouldNotContainHardcodedJwtKey()
    {
        var devSettingsPath = Path.Combine(_repoRoot, "backend", "src", "CulinaryBlog.API", "appsettings.Development.json");
        if (File.Exists(devSettingsPath))
        {
            var content = File.ReadAllText(devSettingsPath);
            content.Should().NotContain("Jwt:Key");
        }
    }

    [Fact]
    public void DockerCompose_ShouldUseEnvironmentInterpolationForSecrets()
    {
        var composePath = Path.Combine(_repoRoot, "docker-compose.yml");
        if (File.Exists(composePath))
        {
            var content = File.ReadAllText(composePath);
            // Jwt__Key must interpolate from environment variable ${JWT_SECRET}
            content.Should().Contain("Jwt__Key: ${JWT_SECRET}");
            content.Should().Contain("${POSTGRES_PASSWORD}");
        }
    }

    [Fact]
    public void ApiCsproj_ShouldContainUserSecretsId()
    {
        var csprojPath = Path.Combine(_repoRoot, "backend", "src", "CulinaryBlog.API", "CulinaryBlog.API.csproj");
        File.Exists(csprojPath).Should().BeTrue();
        var content = File.ReadAllText(csprojPath);
        content.Should().Contain("<UserSecretsId>");
    }
}
