using Xunit;

// Disables cross-collection parallelization: ConsentEndpointTests deliberately runs its own
// CustomWebApplicationFactory outside the "Integration" collection (see its class-level comment),
// and starting two WebApplicationFactory<Program> instances concurrently crashes host startup.
// Running collections sequentially avoids that without forcing ConsentEndpointTests back onto
// the shared rate-limiter bucket that caused it to be split out in the first place.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RentifyxIdentity.Tests.Integration;

[CollectionDefinition("Integration")]
public sealed class IntegrationTests : ICollectionFixture<CustomWebApplicationFactory>;
