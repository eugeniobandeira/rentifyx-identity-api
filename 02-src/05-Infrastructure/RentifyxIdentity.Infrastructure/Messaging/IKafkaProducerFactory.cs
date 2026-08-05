using Confluent.Kafka;

namespace RentifyxIdentity.Infrastructure.Messaging;

public interface IKafkaProducerFactory
{
    IProducer<string, string> Create();
}
