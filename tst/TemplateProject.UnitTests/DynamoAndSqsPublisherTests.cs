using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Configuration;

using NSubstitute;

using TemplateProject.Api;

namespace TemplateProject.UnitTests;

public class DynamoAndSqsPublisherTests
{
    [Fact]
    public async Task Should_Save_Book_To_Dynamo()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        var ctx = new DynamoDBContext(dynamo);
        var repo = new DynamoBookRepository(dynamo);

        var book = new Book { Id = 42, Title = "Async Dynamo", Author = "Test", Year = 2025 };

        await repo.SaveAsync(book);
        await dynamo.Received(1).PutItemAsync(Arg.Any<Amazon.DynamoDBv2.Model.PutItemRequest>());
    }

    [Fact]
    public async Task Should_Publish_Book_To_Sqs()
    {
        var sqs = Substitute.For<IAmazonSQS>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SQS_QUEUE_URL", "http://fake-queue" }
        }).Build();

        var publisher = new SqsPublisher(sqs, config);

        await publisher.PublishAsync(new Book { Id = 1, Title = "From Dynamo to SQS" });

        await sqs.Received(1).SendMessageAsync(Arg.Any<SendMessageRequest>());
    }
}