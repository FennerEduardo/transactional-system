using System.Diagnostics;
using System.Text.Json;
using Npgsql;

namespace transactionalsystem.Runtime;

/// <summary>
/// Idempotent consumer: the message id is recorded in ghk_inbox in the same transaction as the
/// handler, so redeliveries are acknowledged without running the handler twice. A handler error
/// dead-letters the message (RabbitMQ DLX, Pub/Sub / Service Bus dead-letter topic): it is not redelivered.
/// </summary>
public sealed class InboxConsumer
{
    public sealed record Meta(string MessageId, string? TenantId, string? EventType, NpgsqlConnection Connection);

    private readonly NpgsqlDataSource _db;
    private readonly IEventSubscription _subscription;
    private readonly string _name;
    private readonly Func<JsonElement, Meta, Task> _handler;
    private readonly string _s;

    public InboxConsumer(NpgsqlDataSource db, IEventSubscription subscription, string consumerName, Func<JsonElement, Meta, Task> handler, string? schema = null)
    {
        _db = db;
        _subscription = subscription;
        _name = consumerName;
        _handler = handler;
        _s = Schema.Name(schema);
    }

    /// <summary>Processes messages until the source stays empty for <paramref name="idle"/>; returns how many were processed.</summary>
    public async Task<int> DrainAsync(TimeSpan idle, CancellationToken ct = default)
    {
        var processed = 0;
        var idleSince = DateTime.UtcNow;
        while (DateTime.UtcNow - idleSince < idle)
        {
            var delivery = await _subscription.PollAsync(ct);
            if (delivery is null)
            {
                await Task.Delay(50, ct);
                continue;
            }
            await ProcessAsync(delivery);
            processed++;
            idleSince = DateTime.UtcNow;
        }
        return processed;
    }

    private async Task ProcessAsync(Delivery delivery)
    {
        var message = delivery.Message;
        using var activity = Telemetry.Source.StartActivity($"{_subscription.Source} process", ActivityKind.Consumer, Telemetry.ParentFrom(message.Traceparent));
        activity?.SetTag("messaging.system", _subscription.MessagingSystem);
        activity?.SetTag("messaging.destination.name", _subscription.Source);
        activity?.SetTag("messaging.message.id", message.MessageId);
        try
        {
            await using var conn = await _db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            if (await CommandService.Exec(conn, $"INSERT INTO {_s}.ghk_inbox (consumer, message_id) VALUES ($1, $2) ON CONFLICT DO NOTHING", CancellationToken.None, _name, message.MessageId) == 1)
            {
                using var json = JsonDocument.Parse(message.Body);
                await _handler(json.RootElement.Clone(), new Meta(message.MessageId, message.TenantId, message.EventType, conn));
            }
            await tx.CommitAsync();
            await delivery.Ack();
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            await delivery.DeadLetter();
        }
    }
}
