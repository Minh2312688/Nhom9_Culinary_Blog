using CulinaryBlog.Application.Contracts.Recipes;
using Hangfire;

namespace CulinaryBlog.Infrastructure.Images;

public sealed class RecipeImageJobEnqueuer : IRecipeImageJobEnqueuer
{
    private readonly IBackgroundJobClient backgroundJobClient;

    public RecipeImageJobEnqueuer(IBackgroundJobClient backgroundJobClient)
    {
        this.backgroundJobClient = backgroundJobClient;
    }

    public Task EnqueueAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(imageId, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();

        backgroundJobClient.Enqueue<RecipeImageProcessingJob>(
            job => job.ProcessAsync(imageId, CancellationToken.None));
        return Task.CompletedTask;
    }
}
