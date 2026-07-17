using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Infrastructure.Models;
using RentifyxIdentity.Infrastructure.Repositories;
using RentifyxIdentity.Tests.Repositories.Infrastructure;
using Xunit;

namespace RentifyxIdentity.Tests.Repositories.Features.Notifications;

[Trait("Category", "RequiresDocker")]
public sealed class OutboxRepositoryTests : IClassFixture<LocalStackFixture>
{
    private readonly LocalStackFixture _fixture;
    private readonly OutboxRepository _sut;

    public OutboxRepositoryTests(LocalStackFixture fixture)
    {
        _fixture = fixture;
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:DynamoDB:TableName"] = fixture.TableName
            })
            .Build();
        _sut = new OutboxRepository(_fixture.Context, configuration);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingEntries()
    {
        Guid pendingId = await SeedAsync(status: "Pending");
        Guid publishedId = await SeedAsync(status: "Published");

        try
        {
            IReadOnlyList<OutboxEntry> result = await _sut.GetPendingAsync(batchSize: 10);

            result.Select(e => e.Id).Should().Contain(pendingId);
            result.Select(e => e.Id).Should().NotContain(publishedId);
        }
        finally
        {
            await DeleteAsync(pendingId);
            await DeleteAsync(publishedId);
        }
    }

    [Fact]
    public async Task GetPendingAsync_RespectsBatchSize()
    {
        Guid first = await SeedAsync(status: "Pending");
        Guid second = await SeedAsync(status: "Pending");
        Guid third = await SeedAsync(status: "Pending");

        try
        {
            IReadOnlyList<OutboxEntry> result = await _sut.GetPendingAsync(batchSize: 2);

            result.Should().HaveCountLessThanOrEqualTo(2);
        }
        finally
        {
            await DeleteAsync(first);
            await DeleteAsync(second);
            await DeleteAsync(third);
        }
    }

    [Fact]
    public async Task MarkPublishedAsync_UpdatesOnlyTargetedEntry()
    {
        Guid target = await SeedAsync(status: "Pending");
        Guid other = await SeedAsync(status: "Pending");

        try
        {
            await _sut.MarkPublishedAsync(target);

            IReadOnlyList<OutboxEntry> pending = await _sut.GetPendingAsync(batchSize: 10);

            pending.Select(e => e.Id).Should().NotContain(target);
            pending.Select(e => e.Id).Should().Contain(other);
        }
        finally
        {
            await DeleteAsync(target);
            await DeleteAsync(other);
        }
    }

    [Fact]
    public async Task MarkFailedAsync_RemovesEntryFromPending()
    {
        Guid id = await SeedAsync(status: "Pending");

        try
        {
            await _sut.MarkFailedAsync(id);

            IReadOnlyList<OutboxEntry> pending = await _sut.GetPendingAsync(batchSize: 10);

            pending.Select(e => e.Id).Should().NotContain(id);
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task IncrementRetryAsync_IncrementsRetryCountOnlyForTargetedEntry()
    {
        Guid id = await SeedAsync(status: "Pending");

        try
        {
            await _sut.IncrementRetryAsync(id);
            await _sut.IncrementRetryAsync(id);

            IReadOnlyList<OutboxEntry> pending = await _sut.GetPendingAsync(batchSize: 10);
            OutboxEntry? updated = pending.FirstOrDefault(e => e.Id == id);

            updated.Should().NotBeNull();
            updated!.RetryCount.Should().Be(2);
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    private async Task<Guid> SeedAsync(string status)
    {
        Guid id = Guid.NewGuid();
        string pk = $"OUTBOX#{id}";

        OutboxDynamoDbItem item = new()
        {
            Pk = pk,
            Sk = pk,
            Id = id.ToString(),
            TargetTopic = "user-lifecycle-events",
            MessageJson = "{}",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            RetryCount = 0,
            GsiOutboxStatusPk = $"OUTBOX_STATUS#{status}"
        };

        await _fixture.Context.SaveAsync(item, new SaveConfig { OverrideTableName = _fixture.TableName });

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
}
