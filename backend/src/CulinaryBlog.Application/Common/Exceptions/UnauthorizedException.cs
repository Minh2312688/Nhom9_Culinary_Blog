namespace CulinaryBlog.Application.Common.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Invalid email or password.") : base(message)
    {
    }
}
