using CulinaryBlog.Application.Common.Models;
using MediatR;

namespace CulinaryBlog.Application.System.Queries.GetBaseStatus;

public sealed class GetBaseStatusQueryHandler : IRequestHandler<GetBaseStatusQuery, BaseStatusDto>
{
    public Task<BaseStatusDto> Handle(
        GetBaseStatusQuery request,
        CancellationToken cancellationToken)
    {
        var response = new BaseStatusDto(
            Application: "CulinaryBlog.API",
            Environment: request.Environment,
            TimestampUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(response);
    }
}
