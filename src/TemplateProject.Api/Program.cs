var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
var app = builder.Build();
app.UseHttpsRedirection();
app.UseHealthChecks("/health");
app.MapGet("/ping", () =>
{
    return Results.Ok("pong");
});
await app.RunAsync();
namespace TemplateProject.Api
{
    public partial class Program { }
}