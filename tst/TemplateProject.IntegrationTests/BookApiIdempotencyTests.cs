using System.Text;
using System.Text.Json;

namespace TemplateProject.IntegrationTests;

// public class BookApiIdempotencyTests : IClassFixture<ApiFactory>
// {
//     private readonly HttpClient _client;
//
//     public BookApiIdempotencyTests(ApiFactory factory)
//     {
//         _client = factory.CreateClient();
//     }
//
//     [Fact]
//     public async Task Post_With_Same_IdempotencyKey_Should_Return_Same_Result()
//     {
//         var request = new { Title = "IdemBook", Author = "Tester" };
//         var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
//
//         var idemKey = Guid.NewGuid().ToString();
//         _client.DefaultRequestHeaders.Add("Idempotency-Key", idemKey);
//
//         var response1 = await _client.PostAsync("/books", content);
//         var result1 = await response1.Content.ReadAsStringAsync();
//
//         // Reenviar mesma requisição com mesmo idemKey
//         var content2 = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
//         _client.DefaultRequestHeaders.Remove("Idempotency-Key");
//         _client.DefaultRequestHeaders.Add("Idempotency-Key", idemKey);
//         var response2 = await _client.PostAsync("/books", content2);
//         var result2 = await response2.Content.ReadAsStringAsync();
//
//         result1.ShouldBe(result2);
//     }
// }