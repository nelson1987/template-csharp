using System.Text;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SQS;
using Amazon.SQS.Model;

using DotNetEnv;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using StackExchange.Redis;

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
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConn = builder.Configuration["REDIS_URL"] ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConn);
});
builder.Services.AddSingleton<RedisCacheService>();
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
                new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Clean Code") },
            ["author"] =
                new OpenApiSchema
                {
                    Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Robert C. Martin")
                },
            ["year"] = new OpenApiSchema { Type = "integer", Example = new Microsoft.OpenApi.Any.OpenApiInteger(2008) }
        }
    });
    c.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "JWT Token (ex: Bearer eyJ...)",
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
});
var jwtKey = builder.Configuration["JWT_KEY"] ?? "super_secret_key_for_demo";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var config = new AmazonSQSConfig
    {
        ServiceURL = builder.Configuration["SQS_SERVICE_URL"] ?? "http://localhost:4566"
    };
    return new AmazonSQSClient("test", "test", config); // credenciais fake para LocalStack
});

builder.Services.AddScoped<SqsPublisher>();
builder.Services.AddScoped<SqsConsumer>();

builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var cfg = new AmazonDynamoDBConfig
    {
        ServiceURL = builder.Configuration["DYNAMO_URL"] ?? "http://localhost:4566"
    };
    return new AmazonDynamoDBClient("test", "test", cfg);
});
builder.Services.AddScoped<DynamoBookRepository>();

builder.Services.AddHostedService<BookConsumerService>();
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
app.UseAuthentication();
app.UseAuthorization();
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

app.MapPost("/login", (string username, string password) =>
{
    // Demo simples: qualquer usuário com senha "123" é aceito
    if (string.IsNullOrWhiteSpace(username) || password != "123")
        return Results.Unauthorized();

    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(jwtKey);

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject =
            new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("sub", username) }),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials =
            new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return Results.Ok(new { token = tokenHandler.WriteToken(token) });
});

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

// app.MapPost("/books", async (Book book, IBookService s, RedisCacheService cache, SqsPublisher publisher) =>
//     {
//         var created = await s.CreateAsync(book);
//
//         if (created.IsFailure) Results.BadRequest(new { created.Errors });
//
//         await cache.RemoveAsync("books_all");
//
//         await publisher.PublishAsync(new { Action = "BookCreated", Book = created });
//
//         return Results.Created($"/books/{created!.Value!.Id}", created);
//     })
//     .WithName("CreateBook")
//     .WithOpenApi(op => new(op) { Summary = "Criar novo livro", Description = "Cadastra um novo livro no sistema.", })
//     .RequireAuthorization();
app.MapPost("/books", async (Book book, DynamoBookRepository dynamoRepo, SqsPublisher publisher) =>
{
    // salva no Dynamo
    await dynamoRepo.SaveAsync(book);

    // publica evento
    await publisher.PublishAsync(book);

    return Results.Accepted($"/books/{book.Id}", new { message = "Book accepted and queued for processing" });
})
     .WithName("CreateBook")
     .WithOpenApi(op => new(op) { Summary = "Criar novo livro", Description = "Cadastra um novo livro no sistema.", })
     .RequireAuthorization();

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

    [DynamoDBTable("Books")]
    public class Book
    {
        [DynamoDBHashKey] // chave primária
        public int Id { get; set; }

        [DynamoDBProperty] public string Title { get; set; } = "";

        [DynamoDBProperty] public string Author { get; set; } = "";

        [DynamoDBProperty] public int Year { get; set; }
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

    public class RedisCacheService
    {
        private readonly IDatabase _db;
        public RedisCacheService(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        public async Task<T?> GetAsync<T>(string key)
        {
            var val = await _db.StringGetAsync(key);
            return val.HasValue ? JsonSerializer.Deserialize<T>(val!) : default;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, json, ttl);
        }

        public Task RemoveAsync(string key) => _db.KeyDeleteAsync(key);
    }

    public class SqsPublisher
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;

        public SqsPublisher(IAmazonSQS sqs, IConfiguration config)
        {
            _sqs = sqs;
            _queueUrl = config["SQS_QUEUE_URL"] ?? throw new InvalidOperationException("SQS_QUEUE_URL not configured");
        }

        public async Task PublishAsync(object message)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(message);

            var request = new SendMessageRequest { QueueUrl = _queueUrl, MessageBody = json };

            await _sqs.SendMessageAsync(request);
        }
    }

    public class SqsConsumer
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;

        public SqsConsumer(IAmazonSQS sqs, IConfiguration config)
        {
            _sqs = sqs;
            _queueUrl = config["SQS_QUEUE_URL"] ?? throw new InvalidOperationException("SQS_QUEUE_URL not configured");
        }

        public async Task<List<string>> ConsumeAsync(int maxMessages = 5)
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = maxMessages,
                WaitTimeSeconds = 1
            };

            var response = await _sqs.ReceiveMessageAsync(request);

            var bodies = response.Messages.Select(m => m.Body).ToList();

            // Apaga após consumir
            foreach (var msg in response.Messages)
            {
                await _sqs.DeleteMessageAsync(_queueUrl, msg.ReceiptHandle);
            }

            return bodies;
        }
    }

    public class DynamoBookRepository
    {
        private readonly DynamoDBContext _context;

        public DynamoBookRepository(IAmazonDynamoDB dynamoDb)
        {
            _context = new DynamoDBContext(dynamoDb);
        }

        public Task SaveAsync(Book book) => _context.SaveAsync(book);
        public Task<Book?> GetAsync(int id) => _context.LoadAsync<Book>(id);
    }

    public class BookConsumerService : BackgroundService
    {
        private readonly SqsConsumer _consumer;
        private readonly IServiceProvider _sp;
        private readonly RedisCacheService _cache;

        public BookConsumerService(SqsConsumer consumer, IServiceProvider sp, RedisCacheService cache)
        {
            _consumer = consumer;
            _sp = sp;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var messages = await _consumer.ConsumeAsync();
                foreach (var msg in messages)
                {
                    try
                    {
                        var book = JsonSerializer.Deserialize<Book>(msg);
                        if (book is null) continue;

                        using var scope = _sp.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        db.Books.Add(book);
                        await db.SaveChangesAsync(stoppingToken);

                        await _cache.RemoveAsync("books_all");
                        await _cache.SetAsync($"book_{book.Id}", book, TimeSpan.FromMinutes(5));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BookConsumerService] Erro: {ex.Message}");
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}