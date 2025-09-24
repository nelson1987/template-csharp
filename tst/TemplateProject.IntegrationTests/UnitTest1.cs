using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

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
public class UnitTest1 : WebApplicationFixture
{
    [Fact]
    public async Task Test1()
    {
        var webApplication = new WebApplicationFactory<Program>();
        var client = webApplication.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}