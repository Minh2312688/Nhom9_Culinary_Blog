using CulinaryBlog.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common;

public record TestPingCommand(string Value) : IRequest<string>;

public class TestPingCommandValidator : AbstractValidator<TestPingCommand>
{
    public TestPingCommandValidator()
    {
        RuleFor(x => x.Value).NotEmpty().WithMessage("Value is required.");
    }
}

public class BehaviorsTests
{
    [Fact]
    public async Task ValidationBehavior_WhenNoValidators_ShouldCallNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestPingCommand, string>(Enumerable.Empty<IValidator<TestPingCommand>>());
        var command = new TestPingCommand("hello");

        // Act
        var result = await behavior.Handle(command, _ => Task.FromResult("pong"), CancellationToken.None);

        // Assert
        result.Should().Be("pong");
    }

    [Fact]
    public async Task ValidationBehavior_WhenValidationFails_ShouldThrowValidationException()
    {
        // Arrange
        var validator = new TestPingCommandValidator();
        var behavior = new ValidationBehavior<TestPingCommand, string>(new[] { validator });
        var command = new TestPingCommand("");

        // Act
        var act = async () => await behavior.Handle(command, _ => Task.FromResult("pong"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ValidationBehavior_WhenValidationPasses_ShouldCallNext()
    {
        // Arrange
        var validator = new TestPingCommandValidator();
        var behavior = new ValidationBehavior<TestPingCommand, string>(new[] { validator });
        var command = new TestPingCommand("valid-value");

        // Act
        var result = await behavior.Handle(command, _ => Task.FromResult("pong"), CancellationToken.None);

        // Assert
        result.Should().Be("pong");
    }

    [Fact]
    public async Task LoggingBehavior_ShouldLogAndCallNext()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<LoggingBehavior<TestPingCommand, string>>>();
        var behavior = new LoggingBehavior<TestPingCommand, string>(loggerMock.Object);
        var command = new TestPingCommand("test");

        // Act
        var result = await behavior.Handle(command, _ => Task.FromResult("pong"), CancellationToken.None);

        // Assert
        result.Should().Be("pong");
    }

    [Fact]
    public void AddApplication_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(IMediator));
    }
}
