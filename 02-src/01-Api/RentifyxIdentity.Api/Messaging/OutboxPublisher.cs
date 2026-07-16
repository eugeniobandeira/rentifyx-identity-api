using Confluent.Kafka;
using Microsoft.Extensions.Options;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Notifications;
using RentifyxIdentity.Infrastructure.Messaging;
using RentifyxIdentity.Infrastructure.Options;

namespace RentifyxIdentity.Api.Messaging;

/// <summary>
/// IHostedService, PeriodicTimer-driven poll loop over the Outbox: publishes each Pending entry to
/// its TargetTopic, marks Published on ack, increments RetryCount on failure, and marks Failed (logging
/// Critical) once a 3rd consecutive failure would exceed MaxRetryCount. Mirrors comms-api's
/// ReconciliationHostedService polling shape (PeriodicTimer, not DynamoDB Streams - design.md's decision).
/// </summary>
public sealed class OutboxPublisher(
    ILogger<OutboxPublisher> logger,
    IServiceScopeFactory scopeFactory,
    IKafkaProducerFactory producerFactory,
    IOptions<OutboxPublisherOptions> options) : IHostedService, IDisposable
{
    private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(30);

    private readonly OutboxPublisherOptions _options = options.Value;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private IProducer<Null, string>? _producer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _producer = producerFactory.Create();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_timer, _loopCts.Token), CancellationToken.None);

        logger.LogInformation(
            "OutboxPublisher started. PollIntervalSeconds={PollIntervalSeconds} BatchSize={BatchSize} MaxRetryCount={MaxRetryCount}",
            _options.PollIntervalSeconds,
            _options.BatchSize,
            _options.MaxRetryCount);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loopCts is not null)
            await _loopCts.CancelAsync();

        if (_loopTask is not null)
        {
            using CancellationTokenSource timeout = new(ShutdownDrainTimeout);
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                await _loopTask.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                // Best-effort drain within the timeout.
            }
        }

        _loopCts?.Dispose();
        _producer?.Flush(TimeSpan.FromSeconds(5));
        logger.LogInformation("OutboxPublisher stopped");
    }

    private async Task LoopAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await PublishPendingAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task PublishPendingAsync(CancellationToken token)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IOutboxRepository repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        IReadOnlyList<OutboxEntry> pending = await repository.GetPendingAsync(_options.BatchSize, token);

        foreach (OutboxEntry entry in pending)
            await PublishEntryAsync(repository, entry, token);
    }

    private async Task PublishEntryAsync(IOutboxRepository repository, OutboxEntry entry, CancellationToken token)
    {
        try
        {
            await _producer!.ProduceAsync(
                entry.TargetTopic,
                new Message<Null, string> { Value = entry.MessageJson },
                token);

            await repository.MarkPublishedAsync(entry.Id, token);
        }
        catch (Exception ex)
        {
            int attemptNumber = entry.RetryCount + 1;

            if (attemptNumber >= _options.MaxRetryCount)
            {
                await repository.MarkFailedAsync(entry.Id, token);
                logger.LogCritical(
                    ex,
                    "Outbox entry exceeded MaxRetryCount and was marked Failed. Id={Id} TargetTopic={TargetTopic} AttemptNumber={AttemptNumber}",
                    entry.Id,
                    entry.TargetTopic,
                    attemptNumber);
            }
            else
            {
                await repository.IncrementRetryAsync(entry.Id, token);
                logger.LogWarning(
                    ex,
                    "Failed to publish outbox entry, will retry. Id={Id} TargetTopic={TargetTopic} AttemptNumber={AttemptNumber}",
                    entry.Id,
                    entry.TargetTopic,
                    attemptNumber);
            }
        }
    }

    public void Dispose()
    {
        _loopCts?.Dispose();
        _timer?.Dispose();
        _producer?.Dispose();
    }
}
