using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

using TemplateProject.Api;

using Testcontainers.LocalStack;

namespace TemplateProject.IntegrationTests;

public class DynamoToPostgresIntegrationTests : IAsyncLifetime
{
    private readonly LocalStackContainer _localstack;
    private string _queueUrl = default!;
    private IAmazonSQS _sqs = default!;
    private IAmazonDynamoDB _dynamo = default!;

    public DynamoToPostgresIntegrationTests()
    {
        _localstack = new LocalStackBuilder()
            .WithImage("localstack/localstack:3.3")
            .WithEnvironment("SERVICES", "sqs,dynamodb")
            .WithPortBinding(4566, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _localstack.StartAsync();

        var endpoint = $"http://{_localstack.Hostname}:{_localstack.GetMappedPublicPort(4566)}";

        _sqs = new AmazonSQSClient("test", "test", new AmazonSQSConfig { ServiceURL = endpoint });
        _dynamo = new AmazonDynamoDBClient("test", "test", new AmazonDynamoDBConfig { ServiceURL = endpoint });

        // cria fila
        var queueResp = await _sqs.CreateQueueAsync("books-queue");
        _queueUrl = queueResp.QueueUrl;

        // cria tabela Dynamo
        await _dynamo.CreateTableAsync(new CreateTableRequest
        {
            TableName = "Books",
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new("Id", ScalarAttributeType.N)
            },
            KeySchema = new List<KeySchemaElement>
            {
                new("Id", KeyType.HASH)
            },
            ProvisionedThroughput = new ProvisionedThroughput(5, 5)
        });
    }

    public Task DisposeAsync() => _localstack.DisposeAsync().AsTask();

    [Fact]
    public async Task Book_Should_Flow_From_Dynamo_To_Postgres()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SQS_QUEUE_URL", _queueUrl }
        }).Build();

        var publisher = new SqsPublisher(_sqs, config);
        var consumer = new SqsConsumer(_sqs, config);
        var cache = Substitute.For<RedisCacheService>(null!);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            //.UseInMemoryDatabase("books-test")
            .Options;

        var db = new AppDbContext(options);

        // Simula BackgroundService
        var service = new BookConsumerService(consumer, new ServiceCollection()
            .AddSingleton(db)
            .BuildServiceProvider(), cache);

        var book = new Book { Id = 7, Title = "Eventual Consistency", Author = "Tester", Year = 2025 };

        // salva no Dynamo
        var dynamoRepo = new DynamoBookRepository(_dynamo);
        await dynamoRepo.SaveAsync(book);

        // publica no SQS
        await publisher.PublishAsync(book);

        // consome e grava no Postgres
        var consumeTask = service.StartAsync(default);
        await Task.Delay(3000); // tempo p/ consumir
        await service.StopAsync(default);

        db.Books.Count().ShouldBe(1);
        db.Books.Single().Title.ShouldBe("Eventual Consistency");
    }
}