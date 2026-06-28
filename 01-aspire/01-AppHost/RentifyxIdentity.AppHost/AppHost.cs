using Amazon;
using Aspire.Hosting.AWS;
using RentifyxIdentity.AppHost;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IAWSSDKConfig awsConfig = builder.AddAWSSDKConfig()
    .WithRegion(RegionEndpoint.SAEast1);

builder.AddContainer("localstack", "localstack/localstack:3")
    .WithEnvironment("SERVICES", "dynamodb,ses,secretsmanager,kms")
    .WithEnvironment("AWS_DEFAULT_REGION", "sa-east-1")
    .WithEnvironment("LOCALSTACK_HOST", "localhost")
    .WithBindMount("../scripts/init-localstack.sh", "/etc/localstack/init/ready.d/init-aws.sh")
    .WithEndpoint(targetPort: 4566, name: "http");

builder.AddProject<Projects.RentifyxIdentity_Api>("clean-arch-api")
    .WithReference(awsConfig)
    .WithHttpHealthCheck("/health")
    .WithScalar();

await builder.Build().RunAsync();
