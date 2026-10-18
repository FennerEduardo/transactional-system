namespace transactionalsystem.Runtime;

/// <summary>
/// Event bus destinations: events are published to Exchange (RabbitMQ topic exchange / Pub/Sub or Service Bus
/// topic) and consumed from Queue (queue / subscription); dead-lettered messages go to Dlx and are read from Dlq.
/// </summary>
public sealed record Topology(string Exchange, string Queue, string Dlx, string Dlq)
{
    public static readonly Topology Default = new("transactional-system.events", "transactional-system.consumer", "transactional-system.dlx", "transactional-system.dlq");

    public static Topology ForPrefix(string prefix) => new($"{prefix}.events", $"{prefix}.consumer", $"{prefix}.dlx", $"{prefix}.dlq");
}
