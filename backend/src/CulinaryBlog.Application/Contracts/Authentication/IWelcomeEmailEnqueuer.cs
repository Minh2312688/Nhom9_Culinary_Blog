namespace CulinaryBlog.Application.Contracts.Authentication;

public interface IWelcomeEmailEnqueuer
{
    Task EnqueueWelcomeEmailAsync(string email, string displayName, CancellationToken cancellationToken = default);
}
