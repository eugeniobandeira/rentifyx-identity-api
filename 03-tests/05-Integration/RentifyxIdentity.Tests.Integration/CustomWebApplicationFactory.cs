using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RentifyxIdentity.Api.Messaging;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Tests.Common.Constants;
using RentifyxIdentity.Tests.Common.Fakes;
using RentifyxIdentity.Tests.Integration.Auth;

namespace RentifyxIdentity.Tests.Integration;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeUserRepository UserRepository { get; } = new();
    public FakeEmailService EmailService { get; } = new();
    public FakeAuditLogService AuditLogService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [TestConstants.HmacKeyConfigPath] = TestConstants.HmacKey
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });

            services.AddSingleton<IUserRepository>(UserRepository);
            services.AddSingleton<IEmailService>(EmailService);
            services.AddSingleton<ITokenService>(new FakeTokenService());
            services.AddSingleton<IAuditLogService>(AuditLogService);

            // OutboxPublisher's StartAsync calls IKafkaProducerFactory.Create(), which reads
            // configuration.GetConnectionString("kafka") - not present in this test environment, so it
            // throws and aborts the whole host's startup (IHostedService.StartAsync failures are fatal by
            // default), which is why every endpoint test here was failing with "Server hasn't been
            // initialized yet." These HTTP endpoint tests don't need a live outbox publisher running.
            ServiceDescriptor[] outboxPublisherDescriptors = [.. services.Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(OutboxPublisher))];

            foreach (ServiceDescriptor descriptor in outboxPublisherDescriptors)
                services.Remove(descriptor);
        });
    }
}
