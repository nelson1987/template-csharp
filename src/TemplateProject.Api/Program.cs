using DotNetEnv;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using TemplateProject.Api;

var builder = WebApplication.CreateBuilder(args);
Env.Load();
string port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
//string jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "MyApi";
//string jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "MyApiClients";
//string jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET não definido");
//int tokenExpMinutes = int.TryParse(Environment.GetEnvironmentVariable("TOKEN_EXP_MINUTES"), out var t) ? t : 60;

var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
              ?? "Host=localhost;Database=books;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connStr));

builder.Services.AddScoped<IBookRepository, EfBookRepository>();
builder.Services.AddScoped<IBookService, BookService>();
builder.WebHost.UseUrls($"http://*:{port}");
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "Books Minimal API",
            Version = "v1",
            Description = "API minimalista para gerenciar livros 📚 (CRUD completo com EF Core e PostgreSQL)."
        });
    // Adiciona exemplos de request/resposta
    c.MapType<Book>(() => new OpenApiSchema
    {
        Type = "object",
        Properties =
        {
            ["id"] =
                new OpenApiSchema { Type = "integer", Example = new Microsoft.OpenApi.Any.OpenApiInteger(1) },
            ["title"] =
                new OpenApiSchema
                {
                    Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Clean Code")
                },
            ["author"] =
                new OpenApiSchema
                {
                    Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Robert C. Martin")
                },
            ["year"] = new OpenApiSchema
            {
                Type = "integer", Example = new Microsoft.OpenApi.Any.OpenApiInteger(2008)
            }
        }
    });
});
// builder.Services.AddAuthentication()
//     .AddJwtBearer();
// builder.Services.AddAuthorization(o => {
//     o.AddPolicy("ApiTesterPolicy", b => b.RequireRole("tester"));
// });
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Books Minimal API v1");
    c.RoutePrefix = string.Empty; // Swagger abre na raiz
});
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
            entries = report.Entries.Select(e =>
                new { key = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});
app.MapGet("/ping", () => Results.Ok("pong"));

app.MapGet("/books", async (IBookService s) =>
        Results.Ok(await s.GetAllAsync()))
    .WithName("GetAllBooks")
    .WithOpenApi(op => new(op)
    {
        Summary = "Listar todos os livros",
        Description = "Retorna a lista de todos os livros cadastrados."
    });

app.MapGet("/books/{id:int}", async (int id, IBookService s) =>
    {
        var book = await s.GetByIdAsync(id);
        return book is not null ? Results.Ok(book) : Results.NotFound();
    })
    .WithName("GetBookById")
    .WithOpenApi(op => new(op)
    {
        Summary = "Buscar livro por ID",
        Description = "Retorna o livro específico pelo seu ID."
    });

app.MapPost("/books", async (Book book, IBookService s) =>
    {
        var created = await s.CreateAsync(book);
        return created.IsSuccess
            ? Results.Created($"/books/{created!.Value!.Id}", created)
            : Results.BadRequest(new { created.Errors });
    })
    .WithName("CreateBook")
    .WithOpenApi(op => new(op) { Summary = "Criar novo livro", Description = "Cadastra um novo livro no sistema.", });

app.MapPut("/books/{id:int}", async (int id, Book book, IBookService s) =>
    {
        var result = await s.UpdateAsync(id, book);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(new { result.Errors });
    })
    .WithName("UpdateBook")
    .WithOpenApi(op => new(op) { Summary = "Criar novo livro", Description = "Cadastra um novo livro no sistema.", });

app.MapDelete("/books/{id:int}", async (int id, IBookService s) =>
    {
        var deleted = await s.DeleteAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBook")
    .WithOpenApi(op => new(op) { Summary = "Remover livro", Description = "Remove o livro do sistema pelo ID." });


await app.RunAsync();

namespace TemplateProject.Api
{
    public partial class Program
    {
    }

    public class Book
    {
        public int Id { get; set; } // PK
        public string Title { get; set; } = default!;
        public string Author { get; set; } = default!;
        public int Year { get; set; }
    }

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

    public class AppDbContext : DbContext
    {
        public DbSet<Book> Books { get; set; } = default!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Book>().HasKey(b => b.Id);
        }
    }

    public interface IBookRepository
    {
        Task<IEnumerable<Book>> GetAllAsync();
        Task<Book?> GetByIdAsync(int id);
        Task<Book> CreateAsync(Book book);
        Task<bool> UpdateAsync(Book book);
        Task<bool> DeleteAsync(int id);
    }

    public class EfBookRepository : IBookRepository
    {
        private readonly AppDbContext _db;
        public EfBookRepository(AppDbContext db) => _db = db;

        public async Task<Book> CreateAsync(Book book)
        {
            _db.Books.Add(book);
            await _db.SaveChangesAsync();
            return book;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var b = await _db.Books.FindAsync(id);
            if (b is null) return false;
            _db.Books.Remove(b);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Book>> GetAllAsync()
            => await _db.Books.AsNoTracking().ToListAsync();

        public async Task<Book?> GetByIdAsync(int id)
            => await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

        public async Task<bool> UpdateAsync(Book book)
        {
            var exists = await _db.Books.AnyAsync(b => b.Id == book.Id);
            if (!exists) return false;
            _db.Books.Update(book);
            await _db.SaveChangesAsync();
            return true;
        }
    }

    public interface IBookService
    {
        Task<IEnumerable<Book>> GetAllAsync();
        Task<Book?> GetByIdAsync(int id);
        Task<Result<Book?>> CreateAsync(Book book);
        Task<Result> UpdateAsync(int id, Book book);
        Task<bool> DeleteAsync(int id);
    }

    public class BookService(IBookRepository repo) : IBookService
    {
        public Task<IEnumerable<Book>> GetAllAsync() => repo.GetAllAsync();

        public Task<Book?> GetByIdAsync(int id) => repo.GetByIdAsync(id);

        public async Task<Result<Book?>> CreateAsync(Book book)
        {
            if (string.IsNullOrWhiteSpace(book.Title))
                return Result<Book?>.Fail("Title is required");
            if (string.IsNullOrWhiteSpace(book.Author))
                return Result<Book?>.Fail("Author is required");
            var created = await repo.CreateAsync(book);
            return Result<Book?>.Success(created);
        }

        public async Task<Result> UpdateAsync(int id, Book book)
        {
            if (id != book.Id) return Result.Fail("Id mismatch");
            return await repo.UpdateAsync(book)
                ? Result.Ok()
                : Result.Fail("Not found");
        }

        public Task<bool> DeleteAsync(int id) => repo.DeleteAsync(id);
    }
}