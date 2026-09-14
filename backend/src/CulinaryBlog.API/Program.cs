using CulinaryBlog.Application;
using CulinaryBlog.Application.System.Queries.GetBaseStatus;
using CulinaryBlog.Infrastructure;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "CulinaryBlog.API",
    phase = "TV1 - Giai doan 1",
    status = "running"
}));

app.MapGet("/api/v1/system/base-status", async (
    ISender sender,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(
        new GetBaseStatusQuery(environment.EnvironmentName),
        cancellationToken);

    return Results.Ok(result);
});

app.Run();
