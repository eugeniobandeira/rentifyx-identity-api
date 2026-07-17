using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Tests.Common.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, UserEntity> _store = new();

    public List<(string Recipient, string Token)> SentVerificationEmails { get; } = new();
    public List<(string Recipient, string Token)> SentPasswordResetEmails { get; } = new();

    public Task AddAsync(UserEntity entity, CancellationToken ct = default)
        => AddAsync(entity, [], ct);

    public Task AddAsync(UserEntity entity, IReadOnlyCollection<IDomainEvent> extraEvents, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        CaptureRaisedEvents(entity, extraEvents);
        entity.ClearDomainEvents();
        return Task.CompletedTask;
    }

    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out UserEntity? entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(UserEntity entity, CancellationToken ct = default)
        => UpdateAsync(entity, [], ct);

    public Task UpdateAsync(UserEntity entity, IReadOnlyCollection<IDomainEvent> extraEvents, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(entity.Id))
            throw new InvalidOperationException($"Entity {entity.Id} not found for update.");

        _store[entity.Id] = entity;
        CaptureRaisedEvents(entity, extraEvents);
        entity.ClearDomainEvents();
        return Task.CompletedTask;
    }

    private void CaptureRaisedEvents(UserEntity entity, IReadOnlyCollection<IDomainEvent> extraEvents)
    {
        foreach (IDomainEvent domainEvent in entity.DomainEvents.Concat(extraEvents))
        {
            switch (domainEvent)
            {
                case UserRegistered e:
                    SentVerificationEmails.Add((e.Email, e.RawToken));
                    break;
                case PasswordResetRequested e:
                    SentPasswordResetEmails.Add((e.Email, e.RawToken));
                    break;
            }
        }
    }

    public Task DeleteAsync(UserEntity entity, CancellationToken ct = default)
    {
        _store.Remove(entity.Id);
        return Task.CompletedTask;
    }

    public Task<UserEntity?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        UserEntity? match = null;

        foreach (UserEntity entity in _store.Values)
        {
            if (string.Equals(entity.Email.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                match = entity;
                break;
            }
        }

        return Task.FromResult(match);
    }

    public Task<UserEntity?> GetByTaxIdAsync(string taxId, CancellationToken ct = default)
    {
        string normalized = taxId
            .Replace(".", "")
            .Replace("-", "")
            .Replace("/", "")
            .ToUpperInvariant();

        UserEntity? match = null;

        foreach (UserEntity entity in _store.Values)
        {
            if (string.Equals(entity.TaxId.RawValue, normalized, StringComparison.OrdinalIgnoreCase))
            {
                match = entity;
                break;
            }
        }

        return Task.FromResult(match);
    }
}
