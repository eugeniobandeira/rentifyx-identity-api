using Xunit;

namespace RentifyxIdentity.Tests.Integration;

[CollectionDefinition("Integration")]
public sealed class IntegrationTests : ICollectionFixture<CustomWebApplicationFactory>;
