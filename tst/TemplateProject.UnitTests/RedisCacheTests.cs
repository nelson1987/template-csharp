using System.Text.Json;

using NSubstitute;

using Shouldly;

using StackExchange.Redis;

using TemplateProject.Api;

namespace TemplateProject.UnitTests;

public class RedisCacheServiceTests
{
    [Fact]
    public async Task Set_and_Get_object_from_cache()
    {
        var mux = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        mux.GetDatabase().Returns(db);

        var service = new RedisCacheService(mux);

        var key = "book_1";
        var book = new { Id = 1, Title = "Cached" };
        var json = JsonSerializer.Serialize(book);

        // mock DB get/set
        db.StringGetAsync(key, Arg.Any<CommandFlags>())
            .Returns(json);

        await service.SetAsync(key, book, TimeSpan.FromMinutes(5));
        var result = await service.GetAsync<dynamic>(key);

        result.ShouldNotBeNull();
        ((int)result.Id).ShouldBe(1);
    }
}