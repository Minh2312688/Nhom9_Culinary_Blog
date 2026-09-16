using CulinaryBlog.Application.Common.Models;
using MediatR;

namespace CulinaryBlog.Application.System.Queries.GetBaseStatus;

public sealed record GetBaseStatusQuery(string Environment) : IRequest<BaseStatusDto>;
