using System.Text.Json;

using NSubstitute;

using Shouldly;

using StackExchange.Redis;

using TemplateProject.Api;

namespace TemplateProject.UnitTests;

public class IdempotencyTests
{
    private readonly IdempotencyService _service;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public IdempotencyTests()
    {
        _redis = Substitute.For<IConnectionMultiplexer>();
        _db = Substitute.For<IDatabase>();
        _redis.GetDatabase().Returns(_db);

        _service = new IdempotencyService(_redis);
    }

    [Fact]
    public async Task Should_Save_And_Retrieve_Idempotent_Result()
    {
        var key = "idem-123";
        var book = new Book { Title = "Idem Test", Author = "Tester" };
        var serialized = JsonSerializer.Serialize(book);

        _db.StringGetAsync(key, Arg.Any<CommandFlags>())
            .Returns(serialized);

        var (exists, result) = await _service.CheckAsync<Book>(key);

        exists.ShouldBeTrue();
        result.ShouldNotBeNull();
        result!.Title.ShouldBe("Idem Test");
    }

    [Fact]
    public async Task Should_Save_Result_If_Not_Exists()
    {
        var key = "idem-456";
        var book = new Book { Title = "Save Test", Author = "Tester" };

        await _service.SaveAsync(key, book);

        await _db.Received(1).StringSetAsync(
            key,
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()
        );
    }
}