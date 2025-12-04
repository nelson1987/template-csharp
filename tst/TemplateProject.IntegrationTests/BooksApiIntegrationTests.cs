using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using TemplateProject.Api;

using Testcontainers.PostgreSql;

namespace TemplateProject.IntegrationTests;

public class BooksApiIntegrationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgreSqlContainer _pgContainer;
    private HttpClient _client = default!;

    public BooksApiIntegrationTests()
    {
        _pgContainer = new PostgreSqlBuilder()
            .WithDatabase("books")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithImage("postgres:15-alpine")
            .WithCleanUp(true)
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
                });
            });
    }

    public async Task InitializeAsync()
    {
        await _pgContainer.StartAsync();

        _client = _factory.CreateClient();

        // Garante que DB criado
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _pgContainer.DisposeAsync();
    }

    [Fact]
    public async Task Crud_flow_works_with_real_postgres_container()
    {
        // 1) lista inicial vazia
        var list = await _client.GetFromJsonAsync<List<Book>>("/books");
        list!.Count.ShouldBe(0);

        // 2) cria livro
        var newBook = new Book { Title = "Postgres FTW", Author = "Container Dev", Year = 2025 };
        var resp = await _client.PostAsJsonAsync("/books", newBook);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<Book>();
        created!.Id.ShouldBeGreaterThan(0);

        // 3) recupera por ID
        var getResp = await _client.GetAsync($"/books/{created.Id}");
        getResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResp.Content.ReadFromJsonAsync<Book>();
        fetched!.Title.ShouldBe("Postgres FTW");

        // 4) update
        fetched.Author = "Updated Author";
        var putResp = await _client.PutAsJsonAsync($"/books/{fetched.Id}", fetched);
        putResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 5) delete
        var delResp = await _client.DeleteAsync($"/books/{fetched.Id}");
        delResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 6) confirma remoção
        var after = await _client.GetAsync($"/books/{fetched.Id}");
        after.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}