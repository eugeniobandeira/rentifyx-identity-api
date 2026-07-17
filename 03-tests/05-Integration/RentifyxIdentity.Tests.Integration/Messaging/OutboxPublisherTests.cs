using Amazon.DynamoDBv2.Model;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentifyxIdentity.Api.Messaging;
using RentifyxIdentity.Domain.Interfaces.Notifications;
using RentifyxIdentity.Infrastructure.Messaging;
using RentifyxIdentity.Infrastructure.Models;
using RentifyxIdentity.Infrastructure.Options;
using RentifyxIdentity.Infrastructure.Repositories;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Messaging;

[Trait("Category", "RequiresDocker")]
public sealed class OutboxPublisherTests : IClassFixture<OutboxPublisherFixture>
{
    private readonly OutboxPublisherFixture _fixture;

    public OutboxPublisherTests(OutboxPublisherFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PendingEntry_GetsProducedAndMarkedPublished_OnAck()
    {
        Guid id = await SeedAsync(status: "Pending", topic: "user-lifecycle-events");
        OutboxPublisher publisher = BuildPublisher(
            new WorkingKafkaProducerFactory(_fixture.KafkaBootstrapAddress),
            pollIntervalSeconds: 1,
            maxRetryCount: 3);

        try
        {
            await publisher.StartAsync(CancellationToken.None);
            await WaitUntilAsync(async () => await GetStatusAsync(id) == "Published", TimeSpan.FromSeconds(15));

            (await GetStatusAsync(id)).Should().Be("Published");
        }
        finally
        {
            await publisher.StopAsync(CancellationToken.None);
            publisher.Dispose();
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task ProduceFailsThreeTimes_MarksFailed_AndStopsRetrying()
    {
        Guid id = await SeedAsync(status: "Pending", topic: "user-lifecycle-events");
        // Unreachable broker with aggressive timeouts - a real Confluent.Kafka client failing against a real
        // (misconfigured) endpoint, not a mocked failure.
        OutboxPublisher publisher = BuildPublisher(
            new BrokenKafkaProducerFactory(),
            pollIntervalSeconds: 1,
            maxRetryCount: 3);

        try
        {
            await publisher.StartAsync(CancellationToken.None);
            await WaitUntilAsync(async () => await GetStatusAsync(id) == "Failed", TimeSpan.FromSeconds(20));

            (await GetStatusAsync(id)).Should().Be("Failed");

            // Once Failed, GSI_Outbox no longer surfaces it as Pending - no further retries possible.
            IOutboxRepository repository = BuildRepository();
            IReadOnlyList<Domain.Entities.OutboxEntry> pending = await repository.GetPendingAsync(batchSize: 10);
            pending.Select(e => e.Id).Should().NotContain(id);
        }
        finally
        {
            await publisher.StopAsync(CancellationToken.None);
            publisher.Dispose();
            await DeleteAsync(id);
        }
    }

    private OutboxPublisher BuildPublisher(
        IKafkaProducerFactory producerFactory,
        int pollIntervalSeconds,
        int maxRetryCount)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(BuildConfiguration());
        services.AddSingleton(_fixture.Context);
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        ServiceProvider provider = services.BuildServiceProvider();

        IOptions<OutboxPublisherOptions> options = Options.Create(new OutboxPublisherOptions(
            PollIntervalSeconds: pollIntervalSeconds,
            BatchSize: 10,
            MaxRetryCount: maxRetryCount));

        return new OutboxPublisher(
            NullLogger<OutboxPublisher>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            producerFactory,
            options);
    }

    private IOutboxRepository BuildRepository() =>
        new OutboxRepository(_fixture.Context, BuildConfiguration());

    private IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:DynamoDB:TableName"] = _fixture.TableName
            })
            .Build();

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }

    private async Task<string?> GetStatusAsync(Guid id)
    {
        string pk = $"OUTBOX#{id}";
        GetItemResponse response = await _fixture.Client.GetItemAsync(
            _fixture.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = pk },
                ["SK"] = new AttributeValue { S = pk }
            });

        return response.Item is { Count: > 0 } ? response.Item["Status"].S : null;
    }

    private async Task<Guid> SeedAsync(string status, string topic)
    {
        Guid id = Guid.NewGuid();
        string pk = $"OUTBOX#{id}";

        OutboxDynamoDbItem item = new()
        {
            Pk = pk,
            Sk = pk,
            Id = id.ToString(),
            TargetTopic = topic,
            MessageJson = "{}",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            RetryCount = 0,
            GsiOutboxStatusPk = $"OUTBOX_STATUS#{status}"
        };

        await _fixture.Context.SaveAsync(item, new Amazon.DynamoDBv2.DataModel.SaveConfig { OverrideTableName = _fixture.TableName });

        return id;
    }

    private async Task DeleteAsync(Guid id) =>
        await _fixture.Client.DeleteItemAsync(
            _fixture.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"OUTBOX#{id}" },
                ["SK"] = new AttributeValue { S = $"OUTBOX#{id}" }
            });

    private sealed class WorkingKafkaProducerFactory(string bootstrapAddress) : IKafkaProducerFactory
    {
        public IProducer<Null, string> Create() =>
            new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = bootstrapAddress }).Build();
    }

    private sealed class BrokenKafkaProducerFactory : IKafkaProducerFactory
    {
        public IProducer<Null, string> Create() =>
            new ProducerBuilder<Null, string>(new ProducerConfig
            {
                BootstrapServers = "127.0.0.1:1",
                MessageTimeoutMs = 1000,
                SocketTimeoutMs = 500,
                MessageSendMaxRetries = 0
            }).Build();
    }
}
