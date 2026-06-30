using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using RentifyxIdentity.Domain.Entities;
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
        _sut = new UserRepository(_fixture.Context);
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
