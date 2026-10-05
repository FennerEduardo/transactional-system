using RabbitMQ.Client;

namespace transactionalsystem.Runtime;

/// <summary>Topic exchange -> consumer queue, which dead-letters rejected messages to a fanout DLX -> DLQ.</summary>
public sealed record Topology(string Exchange, string Queue, string Dlx, string Dlq)
{
    public static readonly Topology Default = new("transactional-system.events", "transactional-system.consumer", "transactional-system.dlx", "transactional-system.dlq");

    public static Topology ForPrefix(string prefix) => new($"{prefix}.events", $"{prefix}.consumer", $"{prefix}.dlx", $"{prefix}.dlq");

    public void Declare(IModel channel)
    {
        channel.ExchangeDeclare(Exchange, "topic", durable: true);
        channel.ExchangeDeclare(Dlx, "fanout", durable: true);
        channel.QueueDeclare(Dlq, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(Dlq, Dlx, "");
        channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object> { ["x-dead-letter-exchange"] = Dlx });
        channel.QueueBind(Queue, Exchange, "#");
    }
}
