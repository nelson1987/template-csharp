using DotNetEnv;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
Env.Load();
string port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
//string jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "MyApi";
//string jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "MyApiClients";
//string jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET não definido");
//int tokenExpMinutes = int.TryParse(Environment.GetEnvironmentVariable("TOKEN_EXP_MINUTES"), out var t) ? t : 60;
builder.WebHost.UseUrls($"http://*:{port}");
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
var app = builder.Build();
// app.UseAuthentication();
// app.UseAuthorization();
app.UseHttpsRedirection();
app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            totalChecks = report.Entries.Count,
            entries = report.Entries.Select(e => new { key = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});
app.MapGet("/ping", () =>
{
    return Results.Ok("pong");
});
await app.RunAsync();

namespace TemplateProject.Api
{
    public partial class Program { }
    public class Result
    {
        public bool IsSuccess { get; protected set; }
        public bool IsFailure => !IsSuccess;
        public string[] Errors { get; protected set; } = Array.Empty<string>();

        protected Result(bool isSuccess, params string[] errors)
        {
            IsSuccess = isSuccess;
            Errors = errors ?? Array.Empty<string>();
        }

        public static Result Fail(params string[] errors) => new Result(false, errors);
        public static Result Ok() => new Result(true);
    }

    public class Result<T> : Result
    {
        public T? Value { get; private set; }

        protected Result(bool isSuccess, T? value, params string[] errors)
            : base(isSuccess, errors)
        {
            Value = value;
        }

        public static Result<T> Success(T value) => new Result<T>(true, value);
        public static new Result<T> Fail(params string[] errors) => new Result<T>(false, default, errors);
    }
}