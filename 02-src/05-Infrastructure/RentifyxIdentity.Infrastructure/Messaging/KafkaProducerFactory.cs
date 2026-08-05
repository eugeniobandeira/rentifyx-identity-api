using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace RentifyxIdentity.Infrastructure.Messaging;

public sealed class KafkaProducerFactory(IConfiguration configuration) : IKafkaProducerFactory
{
    public IProducer<string, string> Create()
    {
        string bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Connection string 'kafka' not found.");

        ProducerConfig config = new()
        {
            BootstrapServers = bootstrapServers
        };

        return new ProducerBuilder<string, string>(config).Build();
    }
}
