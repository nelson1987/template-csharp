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
app.Run();