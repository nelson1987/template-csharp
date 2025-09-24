using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

using Shouldly;

using TemplateProject.Api;
namespace TemplateProject.IntegrationTests;

public class WebApplicationFixture : WebApplicationFactory<Program>
{
    override protected void ConfigureWebHost(IWebHostBuilder builder)
    {

        builder
        .UseEnvironment("Testing")
        //.UseSetting("https_port", "5001")
        .ConfigureTestServices(services =>
        {
            // customize test services
        });
    }
}
public class PingEndpointTests : IClassFixture<WebApplicationFixture>
{
    private readonly HttpClient _client;

    public PingEndpointTests(WebApplicationFixture factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ShouldReturnPong()
    {
        // Act
        var response = await _client.GetAsync("/ping");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldContain("pong");
    }
}