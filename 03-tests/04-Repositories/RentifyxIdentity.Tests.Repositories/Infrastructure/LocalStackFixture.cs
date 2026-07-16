using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Testcontainers.LocalStack;
using Xunit;

namespace RentifyxIdentity.Tests.Repositories.Infrastructure;

public sealed class LocalStackFixture : IAsyncLifetime
{
    private LocalStackContainer _container = null!;

    public IAmazonDynamoDB Client { get; private set; } = null!;
    public IDynamoDBContext Context { get; private set; } = null!;
    public string TableName { get; } = "rentifyx-identity";

    public async Task InitializeAsync()
    {
        _container = new LocalStackBuilder("localstack/localstack:3")
            .WithEnvironment("SERVICES", "dynamodb")
            .Build();

        await _container.StartAsync();

        string serviceUrl = _container.GetConnectionString();

        // RegionEndpoint must NOT be set alongside ServiceURL here: AWSSDK.DynamoDBv2 (tested on both 4.0.21.7
        // and 4.0.101) rejects every request against LocalStack with "The security token included in the
        // request is invalid." the moment RegionEndpoint is also set - reproduced outside this test project
        // entirely (isolated console repro), so it's an SDK/LocalStack signing interaction, not a fixture bug.
        // ServiceURL alone is sufficient to route requests to the container.
        AmazonDynamoDBConfig config = new()
        {
            ServiceURL = serviceUrl
        };

        Client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            config);

        Context = new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => Client)
            .Build();

        await CreateTableAsync();
        await WaitUntilTableActiveAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _container.StopAsync();
        await _container.DisposeAsync();
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
                new AttributeDefinition { AttributeName = "Email", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "TaxId", AttributeType = ScalarAttributeType.S },
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
                    IndexName = "GSI_Email",
                    KeySchema =
                    [
                        new KeySchemaElement { AttributeName = "Email", KeyType = KeyType.HASH }
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                },
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI_TaxId",
                    KeySchema =
                    [
                        new KeySchemaElement { AttributeName = "TaxId", KeyType = KeyType.HASH }
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                },
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
