namespace CulinaryBlog.Application.Common.Exceptions;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message = "The recipe was changed by another request.")
        : base(message)
    {
    }
}
