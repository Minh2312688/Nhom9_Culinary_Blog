namespace CulinaryBlog.Application.Contracts.Recipes;

public interface IRecipeImageJobEnqueuer
{
    Task EnqueueAsync(Guid imageId, CancellationToken cancellationToken = default);
}
