using CulinaryBlog.Domain.Exceptions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Domain.Exceptions;

public sealed class DomainExceptionTests
{
    [Fact]
    public void DomainRuleViolationException_IsADomainException()
    {
        var exception = new DomainRuleViolationException("Category name is required.");

        Assert.IsAssignableFrom<DomainException>(exception);
        Assert.Equal("Category name is required.", exception.Message);
    }
}
