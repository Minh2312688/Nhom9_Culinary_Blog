using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record DeleteRecipeCommand(Guid Id) : IRequest;
