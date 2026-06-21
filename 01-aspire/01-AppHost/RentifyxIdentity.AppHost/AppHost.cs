using RentifyxIdentity.AppHost;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.RentifyxIdentity_Api>("clean-arch-api")
    .WithHttpHealthCheck("/health")
    .WithScalar();

await builder.Build().RunAsync();
