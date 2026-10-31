using CulinaryBlog.Application.Common.Contracts;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record DeleteRecipeCommand(Guid Id) : IRequest, IAuditCommand
{
    string IAuditCommand.AuditAction => "Recipe.Delete";
    string IAuditCommand.EntityType => "Recipe";
    Guid? IAuditCommand.EntityId => Id;
};
