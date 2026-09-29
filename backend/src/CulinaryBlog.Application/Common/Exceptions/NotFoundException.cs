namespace CulinaryBlog.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message = "Entity not found.") : base(message)
    {
    }
}
