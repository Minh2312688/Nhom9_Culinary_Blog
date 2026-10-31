using CulinaryBlog.Application.Common.Contracts;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record PublishRecipeCommand(Guid Id) : IRequest, IAuditCommand
{
    string IAuditCommand.AuditAction => "Recipe.Publish";
    string IAuditCommand.EntityType => "Recipe";
    Guid? IAuditCommand.EntityId => Id;
};
