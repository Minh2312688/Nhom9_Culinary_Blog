using CulinaryBlog.Infrastructure.Notifications;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Notifications;

public sealed class SmtpOptionsTests
{
    [Fact]
    public void Validate_WithCompleteConfig_ShouldNotThrow()
    {
        // Arrange
        var options = new SmtpOptions
        {
            Host = "localhost",
            Port = 1025,
            FromAddress = "no-reply@culinaryblog.local"
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_WithMissingHost_ShouldThrowClearError_WithoutSecret()
    {
        // Arrange
        var options = new SmtpOptions
        {
            Host = "",
            Port = 1025,
            Password = "super-secret",
            FromAddress = "no-reply@culinaryblog.local"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Smtp:Host");
        ex.Message.Should().NotContain("super-secret");
        ex.ToString().Should().NotContain("super-secret");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void Validate_WithInvalidPort_ShouldThrow(int port)
    {
        // Arrange
        var options = new SmtpOptions
        {
            Host = "localhost",
            Port = port,
            FromAddress = "no-reply@culinaryblog.local"
        };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_WithMissingFromAddress_ShouldThrow()
    {
        // Arrange
        var options = new SmtpOptions { Host = "localhost", Port = 25, FromAddress = "" };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().Throw<InvalidOperationException>();
    }
}
