using CulinaryBlog.Application.Common.Services;
using CulinaryBlog.Application.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common;

public sealed class AuditTrailTests
{
    [Fact]
    public void LogFailure_WritesMetadataWithoutExceptionMessageOrPayload()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(service => service.UserId).Returns("user-42");
        var logger = new ListLogger<AuditTrail>();
        var auditTrail = new AuditTrail(logger, user.Object);

        auditTrail.LogFailure(
            "Auth.RefreshToken.Rotate",
            "RefreshToken",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new InvalidOperationException("secret-token-value"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Auth.RefreshToken.Rotate", entry.Message);
        Assert.Contains("RefreshToken", entry.Message);
        Assert.Contains("user-42", entry.Message);
        Assert.Contains("Failure", entry.Message);
        Assert.Contains(nameof(InvalidOperationException), entry.Message);
        Assert.DoesNotContain("secret-token-value", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogSuccess_WritesAnonymousUserAndSuccessOutcome()
    {
        var user = new Mock<ICurrentUserService>();
        var logger = new ListLogger<AuditTrail>();
        var auditTrail = new AuditTrail(logger, user.Object);

        auditTrail.LogSuccess("RecipeImage.ProcessVariants", "RecipeImage", null);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("RecipeImage.ProcessVariants", entry.Message);
        Assert.Contains("Anonymous", entry.Message);
        Assert.Contains("Success", entry.Message);
    }
}
