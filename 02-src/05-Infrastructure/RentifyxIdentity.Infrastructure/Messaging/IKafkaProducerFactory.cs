using Confluent.Kafka;

namespace RentifyxIdentity.Infrastructure.Messaging;

public interface IKafkaProducerFactory
{
    IProducer<Null, string> Create();
}
