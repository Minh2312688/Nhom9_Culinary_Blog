using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application;
using CulinaryBlog.Infrastructure;

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

app.MapSystemEndpoints();

app.Run();

public partial class Program;
