using NSubstitute;

using Shouldly;

using TemplateProject.Api;

namespace TemplateProject.UnitTests;

public class BookServiceTests
{
    private readonly IBookRepository _repo = Substitute.For<IBookRepository>();
    private readonly BookService _service;

    public BookServiceTests() => _service = new BookService(_repo);

    [Fact]
    public async Task GetAllAsync_returns_all_books()
    {
        // arrange
        var data = new List<Book> { new Book { Id = 1, Title = "A", Author = "X", Year = 2000 } };
        _repo.GetAllAsync().Returns(data);

        // act
        var result = await _service.GetAllAsync();

        // assert
        result.ShouldNotBeNull();
        result.ShouldHaveSingleItem();
        result.ShouldBe(data);
    }

    [Fact]
    public async Task CreateAsync_fails_when_title_empty()
    {
        var book = new Book { Title = "", Author = "Someone", Year = 2020 };
        var created = await _service.CreateAsync(book);

        created.IsSuccess.ShouldBeFalse();
        created.Errors.ShouldContain("Title is required");
        created.Value.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_calls_repo_when_valid()
    {
        var book = new Book { Title = "Good", Author = "Auth", Year = 2022 };
        _repo.CreateAsync(Arg.Any<Book>()).Returns(ci =>
        {
            var b = ci.Arg<Book>();
            b.Id = 42;
            return Task.FromResult(b);
        });

        var created = await _service.CreateAsync(book);

        created.IsSuccess.ShouldBeTrue();
        created.Errors.ShouldBeEmpty();
        created.Value.ShouldNotBeNull();
        created.Value!.Id.ShouldBe(42);

        await _repo.Received(1).CreateAsync(Arg.Is<Book>(b => b.Title == "Good"));
    }

    [Fact]
    public async Task UpdateAsync_returns_false_if_id_mismatch()
    {
        var book = new Book { Id = 5, Title = "X", Author = "Y", Year = 1 };
        var result = await _service.UpdateAsync(6, book);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain("Id mismatch");
    }
}