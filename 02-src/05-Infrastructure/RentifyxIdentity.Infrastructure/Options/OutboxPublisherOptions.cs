namespace RentifyxIdentity.Infrastructure.Options;

public sealed record OutboxPublisherOptions(
    int PollIntervalSeconds = 5,
    int BatchSize = 50,
    int MaxRetryCount = 3);
