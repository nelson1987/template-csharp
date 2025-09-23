var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
var app = builder.Build();
app.UseHttpsRedirection();
app.UseHealthChecks("/health");
app.MapGet("/test", () =>
{
    return Results.Ok("Hello World!");
});
app.Run();