using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Configuration;

using NSubstitute;

using TemplateProject.Api;

namespace TemplateProject.UnitTests;

public class SqsPublisherTests
{
    [Fact]
    public async Task PublishAsync_Should_Call_Sqs_SendMessage()
    {
        var sqs = Substitute.For<IAmazonSQS>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SQS_QUEUE_URL", "http://fake-queue" }
        }).Build();

        var publisher = new SqsPublisher(sqs, config);

        await publisher.PublishAsync(new { Id = 1, Title = "Test" });

        await sqs.Received(1).SendMessageAsync(
            Arg.Is<SendMessageRequest>(r => r.QueueUrl == "http://fake-queue"),
            Arg.Any<CancellationToken>());
    }
}