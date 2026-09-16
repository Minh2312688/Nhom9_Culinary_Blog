using CulinaryBlog.Application.DTOs.System;
using MediatR;

namespace CulinaryBlog.Application.Features.System.Queries.GetBaseStatus;

public sealed record GetBaseStatusQuery(string Environment) : IRequest<BaseStatusDto>;
