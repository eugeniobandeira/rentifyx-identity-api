using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Testcontainers.LocalStack;
using Xunit;

namespace RentifyxIdentity.Tests.Repositories.Infrastructure;

public sealed class LocalStackFixture : IAsyncLifetime
{
    private LocalStackContainer _container = null!;

    public IAmazonDynamoDB Client { get; private set; } = null!;
    public string TableName { get; } = "rentifyx-identity";

    public async Task InitializeAsync()
    {
        _container = new LocalStackBuilder("localstack/localstack:latest")
            .WithEnvironment("SERVICES", "dynamodb")
            .Build();

        await _container.StartAsync();

        string serviceUrl = _container.GetConnectionString();

        AmazonDynamoDBConfig config = new()
        {
            ServiceURL = serviceUrl,
            RegionEndpoint = RegionEndpoint.SAEast1
        };

        Client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            config);

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
                new AttributeDefinition { AttributeName = "GSI_Email_PK", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "GSI_TaxId_PK", AttributeType = ScalarAttributeType.S }
            ],
            KeySchema =
            [
                new KeySchemaElement { AttributeName = "PK", KeyType = KeyType.HASH }
            ],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI_Email",
                    KeySchema =
                    [
                        new KeySchemaElement { AttributeName = "GSI_Email_PK", KeyType = KeyType.HASH }
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                },
                new GlobalSecondaryIndex
                {
                    IndexName = "GSI_TaxId",
                    KeySchema =
                    [
                        new KeySchemaElement { AttributeName = "GSI_TaxId_PK", KeyType = KeyType.HASH }
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
