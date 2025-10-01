using Amazon.SQS;

using Microsoft.Extensions.Configuration;

using Shouldly;

using TemplateProject.Api;

using Testcontainers.LocalStack;

namespace TemplateProject.IntegrationTests;

public class SqsIntegrationTests : IAsyncLifetime
{
    private readonly LocalStackContainer _localstack;
    private IAmazonSQS _sqs = default!;
    private string _queueUrl = default!;

    public SqsIntegrationTests()
    {
        _localstack = new LocalStackBuilder()
            .WithImage("localstack/localstack:3.3")
            .WithEnvironment("SERVICES", "sqs")
            .WithPortBinding(4566, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _localstack.StartAsync();

        var sqsConfig = new AmazonSQSConfig
        {
            ServiceURL = $"http://{_localstack.Hostname}:{_localstack.GetMappedPublicPort(4566)}"
        };

        _sqs = new AmazonSQSClient("test", "test", sqsConfig);

        var createResp = await _sqs.CreateQueueAsync("books-queue");
        _queueUrl = createResp.QueueUrl;
    }

    public Task DisposeAsync() => _localstack.DisposeAsync().AsTask();

    [Fact]
    public async Task Should_Publish_And_Consume_Message()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "SQS_QUEUE_URL", _queueUrl } }).Build();

        var publisher = new SqsPublisher(_sqs, config);
        var consumer = new SqsConsumer(_sqs, config);

        await publisher.PublishAsync(new { Event = "BookCreated", Title = "Kafka on the Shore" });

        var messages = await consumer.ConsumeAsync();
        messages.ShouldNotBeEmpty();
        messages[0].ShouldContain("BookCreated");
    }
}