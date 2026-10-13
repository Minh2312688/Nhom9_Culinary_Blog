using CulinaryBlog.Application.Contracts.Recipes;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Images;

internal sealed class NoOpRecipeImageJobEnqueuer : IRecipeImageJobEnqueuer
{
    private readonly ILogger<NoOpRecipeImageJobEnqueuer> logger;

    public NoOpRecipeImageJobEnqueuer(ILogger<NoOpRecipeImageJobEnqueuer> logger)
    {
        this.logger = logger;
    }

    public Task EnqueueAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogWarning(
            "Recipe image {ImageId} variant processing was NOT enqueued: Hangfire is not registered in this environment.",
            imageId);
        return Task.CompletedTask;
    }
}
