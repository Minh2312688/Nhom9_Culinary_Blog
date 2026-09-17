using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Application;
using CulinaryBlog.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
// Đăng ký các service của các layer
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
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
app.UseAuthentication();
app.UseAuthorization();
// Đăng ký tất cả endpoints
app.MapCategoryEndpoints();
// app.MapRecipeEndpoints();
app.Run();
