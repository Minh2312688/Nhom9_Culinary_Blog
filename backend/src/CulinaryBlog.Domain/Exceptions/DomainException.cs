namespace CulinaryBlog.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message);

public sealed class DomainRuleViolationException(string message) : DomainException(message);
