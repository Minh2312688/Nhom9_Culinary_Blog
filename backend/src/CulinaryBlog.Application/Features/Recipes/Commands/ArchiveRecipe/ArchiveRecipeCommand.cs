using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record ArchiveRecipeCommand(Guid Id) : IRequest;
