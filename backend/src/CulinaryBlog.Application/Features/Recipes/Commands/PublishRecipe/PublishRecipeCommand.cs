using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record PublishRecipeCommand(Guid Id) : IRequest;
