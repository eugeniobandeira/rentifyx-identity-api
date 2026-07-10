using Amazon;
using Aspire.Hosting.AWS;
using RentifyxIdentity.AppHost;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IAWSSDKConfig awsConfig = builder.AddAWSSDKConfig()
    .WithRegion(RegionEndpoint.SAEast1);

builder.AddProject<Projects.RentifyxIdentity_Api>("clean-arch-api")
    .WithReference(awsConfig)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
    .WithHttpHealthCheck("/health")
    .WithScalar();

await builder.Build().RunAsync();
