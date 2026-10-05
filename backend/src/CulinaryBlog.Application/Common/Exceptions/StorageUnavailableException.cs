namespace CulinaryBlog.Application.Common.Exceptions;

public sealed class StorageUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);