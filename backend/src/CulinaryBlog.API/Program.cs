using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
// Đăng ký các service của các layer
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CulinaryBlog.Application.Contracts.ICurrentUserService, CulinaryBlog.API.CurrentUserService>();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
// Đăng ký OpenAPI — .NET 10 native, không cần Swashbuckle
builder.Services.AddOpenApi(options =>
{
 options.AddDocumentTransformer((document, context, ct) =>
 {
 // Thông tin cơ bản của API
 document.Info = new()
 {
 Title = "Culinary Blog API",
Version = "v1",
 Description = "API cho ứng dụng Blog ẩm thực và nấu ăn",
 };
 return Task.CompletedTask;
 });
});
var app = builder.Build();
if (app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RandomDataSeeder");
    await dbContext.Database.MigrateAsync();
    await RandomDataSeeder.SeedAsync(dbContext, logger);
}
if (app.Environment.IsDevelopment())
{
 // Endpoint phục vụ OpenAPI spec (JSON/YAML)
 app.MapOpenApi();
 // Scalar UI — thay thế Swagger UI trong .NET 10
 app.MapScalarApiReference(options =>
 {
 options
 .WithTitle("Culinary Blog API")
 .WithTheme(ScalarTheme.Purple) // Giao diện tím phù hợp với food blog
 .WithDefaultHttpClient(ScalarTarget.CSharp,
ScalarClient.HttpClient);
 });
}
app.UseHttpsRedirection();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
 var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>()?.Error;
 var status = exception switch
 {
  ForbiddenAccessException => StatusCodes.Status403Forbidden,
  NotFoundException => StatusCodes.Status404NotFound,
  ConcurrencyException => StatusCodes.Status409Conflict,
  ValidationException => StatusCodes.Status400BadRequest,
  InvalidOperationException => StatusCodes.Status400BadRequest,
  _ => StatusCodes.Status500InternalServerError
 };
 context.Response.StatusCode = status;
 await Results.Problem(statusCode: status, detail: exception?.Message).ExecuteAsync(context);
}));
app.UseAuthentication();
app.UseAuthorization();
// Đăng ký tất cả endpoints
app.MapCategoryEndpoints();
app.MapSystemEndpoints();
app.MapRecipeEndpoints();
app.MapGet("/", () => Results.Ok(new { status = "Running" }));
app.Run();
