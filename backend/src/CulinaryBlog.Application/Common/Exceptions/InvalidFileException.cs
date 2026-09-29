namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// File upload bị từ chối ở tầng validation. Code cho biết lý do cụ thể
/// (xem CulinaryBlog.Application.Contracts.Storage.FileErrorCodes) để tầng API map sang ProblemDetails 400.
/// </summary>
public sealed class InvalidFileException : Exception
{
    public InvalidFileException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public InvalidFileException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
