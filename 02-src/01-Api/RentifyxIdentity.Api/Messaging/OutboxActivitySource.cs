using System.Diagnostics;

namespace RentifyxIdentity.Api.Messaging;

internal static class OutboxActivitySource
{
    internal const string Name = "RentifyxIdentity.Outbox";

    internal static readonly ActivitySource Instance = new(Name);
}
