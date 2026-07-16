using Amazon;
using Aspire.Hosting.AWS;
using RentifyxIdentity.AppHost;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string awsRegion = builder.Configuration["AWS:Region"] ?? "sa-east-1";

IAWSSDKConfig awsConfig = builder.AddAWSSDKConfig()
    .WithRegion(RegionEndpoint.GetBySystemName(awsRegion));

IResourceBuilder<KafkaServerResource> kafka = builder
    .AddKafka("kafka")
    .WithKafkaUI();

builder.AddProject<Projects.RentifyxIdentity_Api>("rentifyx-identity-api")
    .WithReference(awsConfig)
    .WithReference(kafka)
    .WithHttpHealthCheck("/health")
    .WithScalar();

await builder.Build().RunAsync();
