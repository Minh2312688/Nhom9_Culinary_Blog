namespace CulinaryBlog.Application.Common.Exceptions;

public class InvalidGoogleTokenException : Exception
{
    public InvalidGoogleTokenException(string message = "Invalid or expired Google token.") : base(message)
    {
    }
}
