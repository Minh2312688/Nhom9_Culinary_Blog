namespace CulinaryBlog.Domain.Exceptions;

/// <summary>
/// Thrown when an operation violates the domain lifecycle rules of a RefreshToken.
/// </summary>
public class InvalidTokenStateException : DomainException
{
    public InvalidTokenStateException(string message) : base(message)
    {
    }
}
