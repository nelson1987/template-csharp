using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using StackExchange.Redis;

using TemplateProject.Api;

using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace TemplateProject.IntegrationTests;

public class BooksJwtIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pgContainer;
    private readonly RedisContainer _redisContainer;
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = default!;

    public BooksJwtIntegrationTests()
    {
        _pgContainer = new PostgreSqlBuilder()
            .WithDatabase("books")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithImage("postgres:15-alpine")
            .WithCleanUp(true)
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .Build();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(_pgContainer.GetConnectionString()));

                    // Redis
                    services.AddSingleton<IConnectionMultiplexer>(sp =>
                    {
                        var conn = $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(6379)}";
                        return ConnectionMultiplexer.Connect(conn);
                    });
                });
            });
    }

    public async Task InitializeAsync()
    {
        await _pgContainer.StartAsync();
        await _redisContainer.StartAsync();

        _client = _factory.CreateClient();

        // prepara DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // login e setar Bearer token
        var loginResp = await _client.PostAsJsonAsync("/login", new { username = "test", password = "123" });
        loginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var loginData = await loginResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginData!["token"]);
    }

    public async Task DisposeAsync()
    {
        await _pgContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task Crud_flow_with_jwt_and_redis_cache()
    {
        // lista inicial
        var list = await _client.GetFromJsonAsync<List<Book>>("/books");
        list!.Count.ShouldBe(0);

        // cria livro
        var book = new Book { Title = "JWT Redis", Author = "Tester", Year = 2025 };
        var resp = await _client.PostAsJsonAsync("/books", book);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<Book>();
        created.ShouldNotBeNull();

        // busca -> primeiro hit DB, depois cache
        var get1 = await _client.GetAsync($"/books/{created!.Id}");
        get1.StatusCode.ShouldBe(HttpStatusCode.OK);

        var get2 = await _client.GetAsync($"/books/{created.Id}");
        get2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}