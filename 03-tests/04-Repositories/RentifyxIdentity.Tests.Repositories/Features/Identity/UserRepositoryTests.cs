using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Application.Outbox;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Events;
using Xunit;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Infrastructure.Repositories;
using RentifyxIdentity.Tests.Repositories.Infrastructure;

namespace RentifyxIdentity.Tests.Repositories.Features.Identity;

[Trait("Category", "RequiresDocker")]
public sealed class UserRepositoryTests : IClassFixture<LocalStackFixture>
{
    private readonly LocalStackFixture _fixture;
    private readonly UserRepository _sut;

    public UserRepositoryTests(LocalStackFixture fixture)
    {
        _fixture = fixture;
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:DynamoDB:TableName"] = fixture.TableName
            })
            .Build();
        _sut = new UserRepository(_fixture.Context, _fixture.Client, new OutboxEntryFactory(), configuration);
    }

    [Fact]
    public async Task Add_ValidUser_PersistsAllFields()
    {
        UserEntity user = BuildUser("persist@example.com", "11122233344");

        try
        {
            await _sut.AddAsync(user);

            UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);

            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(user.Id);
            retrieved.Email.Value.Should().Be(user.Email.Value);
            retrieved.TaxId.RawValue.Should().Be(user.TaxId.RawValue);
            retrieved.PasswordHash.HashValue.Should().Be(user.PasswordHash.HashValue);
            retrieved.Role.Should().Be(user.Role);
            retrieved.Status.Should().Be(user.Status);
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNull()
    {
        UserEntity? result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmail_ExistingEmail_ReturnsUser()
    {
        UserEntity user = BuildUser("byemail@example.com", "22233344455");

        try
        {
            await _sut.AddAsync(user);

            UserEntity? result = await _sut.GetByEmailAsync("byemail@example.com");

            result.Should().NotBeNull();
            result!.Id.Should().Be(user.Id);
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task GetByEmail_NonExistentEmail_ReturnsNull()
    {
        UserEntity? result = await _sut.GetByEmailAsync("nobody@example.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTaxId_ExistingTaxId_ReturnsUser()
    {
        UserEntity user = BuildUser("bytaxid@example.com", "33344455566");

        try
        {
            await _sut.AddAsync(user);

            UserEntity? result = await _sut.GetByTaxIdAsync("33344455566");

            result.Should().NotBeNull();
            result!.Id.Should().Be(user.Id);
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task Update_AfterVerifyEmail_StatusIsActive()
    {
        UserEntity user = BuildUser("verify@example.com", "44455566677");

        try
        {
            await _sut.AddAsync(user);

            user.VerifyEmail();
            await _sut.UpdateAsync(user);

            UserEntity? updated = await _sut.GetByIdAsync(user.Id);

            updated.Should().NotBeNull();
            updated!.Status.Should().Be(UserStatus.Active);
            updated.EmailVerificationTokenHash.Should().BeNull();
            updated.EmailVerificationTokenExpiry.Should().BeNull();
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task Add_PendingVerificationUser_HasTtlAttribute()
    {
        UserEntity user = BuildUser("ttlpending@example.com", "55566677788");

        try
        {
            await _sut.AddAsync(user);

            GetItemResponse raw = await _fixture.Client.GetItemAsync(
                _fixture.TableName,
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"USER#{user.Id}" },
                    ["SK"] = new AttributeValue { S = $"USER#{user.Id}" }
                });

            raw.Item.Should().ContainKey("TTL");
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task Update_ActiveUser_HasNoTtlAttribute()
    {
        UserEntity user = BuildUser("ttlactive@example.com", "66677788899");

        try
        {
            await _sut.AddAsync(user);

            user.VerifyEmail();
            await _sut.UpdateAsync(user);

            GetItemResponse raw = await _fixture.Client.GetItemAsync(
                _fixture.TableName,
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"USER#{user.Id}" },
                    ["SK"] = new AttributeValue { S = $"USER#{user.Id}" }
                });

            raw.Item.Should().NotContainKey("TTL");
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsFailedLoginAttempts()
    {
        UserEntity user = BuildUser("lockout-attempts@example.com", "33344455566");

        try
        {
            await _sut.AddAsync(user);
            user.RecordFailedLogin(DateTimeOffset.UtcNow);
            user.RecordFailedLogin(DateTimeOffset.UtcNow);
            user.RecordFailedLogin(DateTimeOffset.UtcNow);
            await _sut.UpdateAsync(user);

            UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);

            retrieved.Should().NotBeNull();
            retrieved!.FailedLoginAttempts.Should().Be(3);
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsLockoutUntil()
    {
        UserEntity user = BuildUser("lockout-until@example.com", "44455566677");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try
        {
            await _sut.AddAsync(user);
            for (int i = 0; i < 5; i++)
                user.RecordFailedLogin(now);
            await _sut.UpdateAsync(user);

            UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);

            retrieved.Should().NotBeNull();
            retrieved!.LockoutUntil.Should().NotBeNull();
            retrieved.LockoutUntil!.Value.Should().BeCloseTo(
                now.AddMinutes(15),
                TimeSpan.FromSeconds(2));
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsGrantedEssentialConsent()
    {
        UserEntity user = BuildUser("consent-grant@example.com", "77788899900");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try
        {
            await _sut.AddAsync(user);

            user.RevokeEssentialConsent(now);
            user.GrantEssentialConsent(now);
            await _sut.UpdateAsync(user);

            UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);

            retrieved.Should().NotBeNull();
            retrieved!.ConsentGivenAt.Should().NotBeNull();
            retrieved.ConsentGivenAt!.Value.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));
            retrieved.EssentialConsentRevokedAt.Should().BeNull();
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsRevokedEssentialConsent()
    {
        UserEntity user = BuildUser("consent-revoke@example.com", "88899900011");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try
        {
            await _sut.AddAsync(user);

            user.RevokeEssentialConsent(now);
            await _sut.UpdateAsync(user);

            UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);

            retrieved.Should().NotBeNull();
            retrieved!.ConsentGivenAt.Should().BeNull();
            retrieved.EssentialConsentRevokedAt.Should().NotBeNull();
            retrieved.EssentialConsentRevokedAt!.Value.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsMarketingConsentGrantAndRevoke()
    {
        UserEntity user = BuildUser("marketing-consent@example.com", "99900011122");
        DateTimeOffset grantedAt = DateTimeOffset.UtcNow;

        try
        {
            await _sut.AddAsync(user);

            user.GrantMarketingConsent(grantedAt);
            await _sut.UpdateAsync(user);

            UserEntity? afterGrant = await _sut.GetByIdAsync(user.Id);

            afterGrant.Should().NotBeNull();
            afterGrant!.MarketingConsentGivenAt.Should().NotBeNull();
            afterGrant.MarketingConsentGivenAt!.Value.Should().BeCloseTo(grantedAt, TimeSpan.FromSeconds(2));

            DateTimeOffset revokedAt = DateTimeOffset.UtcNow;
            afterGrant.RevokeMarketingConsent(revokedAt);
            await _sut.UpdateAsync(afterGrant);

            UserEntity? afterRevoke = await _sut.GetByIdAsync(user.Id);

            afterRevoke.Should().NotBeNull();
            afterRevoke!.MarketingConsentGivenAt.Should().BeNull();
            afterRevoke.MarketingConsentRevokedAt.Should().NotBeNull();
            afterRevoke.MarketingConsentRevokedAt!.Value.Should().BeCloseTo(revokedAt, TimeSpan.FromSeconds(2));
        }
        finally
        {
            await DeleteUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task AddAsync_WithExtraEvents_WritesUserAndOutboxItemsAtomically()
    {
        UserEntity user = BuildUser("outbox-atomic@example.com", "12312312312");
        PasswordResetRequested resetEvent = new(user.Id, user.Email.Value, "raw-token", DateTimeOffset.UtcNow);

        try
        {
            await _sut.AddAsync(user, [resetEvent]);

            UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);
            retrieved.Should().NotBeNull();

            List<Dictionary<string, AttributeValue>> outboxItems = await ScanOutboxItemsForUserAsync(user.Id);
            outboxItems.Should().ContainSingle();
        }
        finally
        {
            await DeleteUserAsync(user.Id);
            await DeleteOutboxItemsForUserAsync(user.Id);
        }
    }

    [Fact]
    public async Task AddAsync_TransactionFailsOnOutboxWrite_RollsBackUserItemToo()
    {
        UserEntity user = BuildUser("outbox-rollback@example.com", "45645645645");
        // DynamoDB rejects items over 400KB - forces TransactWriteItemsAsync to fail on the Outbox Put while
        // the User Put in the same transaction is perfectly valid, proving atomicity (not a mocked failure).
        PasswordResetRequested oversizedEvent = new(user.Id, user.Email.Value, new string('a', 450_000), DateTimeOffset.UtcNow);

        Func<Task> act = () => _sut.AddAsync(user, [oversizedEvent]);

        await act.Should().ThrowAsync<Exception>();

        UserEntity? retrieved = await _sut.GetByIdAsync(user.Id);
        retrieved.Should().BeNull("the transaction must roll back the user item when the outbox write fails");

        List<Dictionary<string, AttributeValue>> outboxItems = await ScanOutboxItemsForUserAsync(user.Id);
        outboxItems.Should().BeEmpty();
    }

    private async Task<List<Dictionary<string, AttributeValue>>> ScanOutboxItemsForUserAsync(Guid userId)
    {
        ScanResponse response = await _fixture.Client.ScanAsync(new ScanRequest
        {
            TableName = _fixture.TableName,
            FilterExpression = "begins_with(PK, :prefix) AND contains(MessageJson, :uid)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":prefix"] = new AttributeValue { S = "OUTBOX#" },
                [":uid"] = new AttributeValue { S = userId.ToString() }
            }
        });

        return response.Items;
    }

    private async Task DeleteOutboxItemsForUserAsync(Guid userId)
    {
        List<Dictionary<string, AttributeValue>> items = await ScanOutboxItemsForUserAsync(userId);

        foreach (Dictionary<string, AttributeValue> item in items)
        {
            await _fixture.Client.DeleteItemAsync(
                _fixture.TableName,
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = item["PK"],
                    ["SK"] = item["SK"]
                });
        }
    }

    private static UserEntity BuildUser(
        string email,
        string taxId)
    {
        return UserEntity.Create(
            email: Email.Create(email),
            taxId: TaxDocument.Create(taxId),
            passwordHash: Password.FromHash("$2a$12$hashedvaluefortesting............."),
            role: UserRole.Renter);
    }

    private async Task DeleteUserAsync(Guid id)
    {
        await _fixture.Client.DeleteItemAsync(
            _fixture.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"USER#{id}" },
                ["SK"] = new AttributeValue { S = $"USER#{id}" }
            });
    }
}
