using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Testcontainers.Kafka;
using Testcontainers.LocalStack;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Messaging;

/// <summary>
/// Real LocalStack (DynamoDB, with GSI_Outbox) + real Kafka container, shared per test class. Mirrors
/// 04-Repositories' LocalStackFixture table shape - kept separate rather than referenced across test
/// projects since no shared test-fixture project exists in this repo yet.
/// </summary>
public sealed class OutboxPublisherFixture : IAsyncLifetime
{
    private LocalStackContainer _localStack = null!;
    private KafkaContainer _kafka = null!;

    public IAmazonDynamoDB Client { get; private set; } = null!;
    public IDynamoDBContext Context { get; private set; } = null!;
    public string TableName { get; } = "rentifyx-identity";
    public string KafkaBootstrapAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _localStack = new LocalStackBuilder("localstack/localstack:3")
            .WithEnvironment("SERVICES", "dynamodb")
            .Build();
        _kafka = new KafkaBuilder("confluentinc/cp-kafka:7.6.0").Build();

        await Task.WhenAll(_localStack.StartAsync(), _kafka.StartAsync());

        KafkaBootstrapAddress = _kafka.GetBootstrapAddress();

        // RegionEndpoint must NOT be set alongside ServiceURL - see 04-Repositories/Infrastructure/LocalStackFixture.cs
        // for the reproduction: AWSSDK.DynamoDBv2 rejects every request against LocalStack with "The security
        // token included in the request is invalid" the moment RegionEndpoint is also set.
        Client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = _localStack.GetConnectionString() });

        Context = new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => Client)
            .Build();

        await CreateTableAsync();
        await WaitUntilTableActiveAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Task.WhenAll(_localStack.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask());
    }

    private async Task CreateTableAsync()
    {
        CreateTableRequest request = new()
        {
            TableName = TableName,
            AttributeDefinitions =
            [
                new AttributeDefinition { AttributeName = "PK", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "SK", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "GsiOutboxStatusPk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "CreatedAt", AttributeType = ScalarAttributeType.S }
            ],
            KeySchema =
            [
                new KeySchemaElement { AttributeName = "PK", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "SK", KeyType = KeyType.RANGE }
            ],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI_Outbox",
                    KeySchema =
                    [
                        new KeySchemaElement { AttributeName = "GsiOutboxStatusPk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "CreatedAt", KeyType = KeyType.RANGE }
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await Client.CreateTableAsync(request);
    }

    private async Task WaitUntilTableActiveAsync()
    {
        while (true)
        {
            DescribeTableResponse response = await Client.DescribeTableAsync(TableName);

            if (response.Table.TableStatus == TableStatus.ACTIVE)
                break;

            await Task.Delay(200);
        }
    }
}
