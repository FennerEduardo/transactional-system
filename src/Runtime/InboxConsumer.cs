using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Npgsql;
using RabbitMQ.Client;

namespace transactionalsystem.Runtime;

/// <summary>
/// Idempotent consumer: the message id is recorded in ghk_inbox in the same transaction as the
/// handler, so redeliveries are acknowledged without running the handler twice. A handler error
/// rejects the message without requeue, so RabbitMQ dead-letters it to the DLQ.
/// </summary>
public sealed class InboxConsumer
{
    public sealed record Meta(string MessageId, string? TenantId, string? EventType, NpgsqlConnection Connection);

    private readonly NpgsqlDataSource _db;
    private readonly IModel _channel;
    private readonly string _queue;
    private readonly string _name;
    private readonly Func<JsonElement, Meta, Task> _handler;
    private readonly string _s;

    public InboxConsumer(NpgsqlDataSource db, IModel channel, string queue, string consumerName, Func<JsonElement, Meta, Task> handler, string? schema = null)
    {
        _db = db;
        _channel = channel;
        _queue = queue;
        _name = consumerName;
        _handler = handler;
        _s = Schema.Name(schema);
    }

    /// <summary>Processes messages until the queue stays empty for <paramref name="idle"/>; returns how many were processed.</summary>
    public async Task<int> DrainAsync(TimeSpan idle)
    {
        var processed = 0;
        var idleSince = DateTime.UtcNow;
        while (DateTime.UtcNow - idleSince < idle)
        {
            var message = _channel.BasicGet(_queue, autoAck: false);
            if (message is null)
            {
                await Task.Delay(50);
                continue;
            }
            await ProcessAsync(message);
            processed++;
            idleSince = DateTime.UtcNow;
        }
        return processed;
    }

    private static string? Header(IBasicProperties props, string key) =>
        props.Headers is not null && props.Headers.TryGetValue(key, out var value)
            ? value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString()
            : null;

    private async Task ProcessAsync(BasicGetResult message)
    {
        var props = message.BasicProperties;
        using var activity = Telemetry.Source.StartActivity($"{_queue} process", ActivityKind.Consumer, Telemetry.ParentFrom(Header(props, "traceparent")));
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", _queue);
        activity?.SetTag("messaging.message.id", props.MessageId);
        try
        {
            await using var conn = await _db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            if (await CommandService.Exec(conn, $"INSERT INTO {_s}.ghk_inbox (consumer, message_id) VALUES ($1, $2) ON CONFLICT DO NOTHING", CancellationToken.None, _name, props.MessageId) == 1)
            {
                using var json = JsonDocument.Parse(message.Body);
                await _handler(json.RootElement.Clone(), new Meta(props.MessageId, Header(props, "tenant_id"), props.Type, conn));
            }
            await tx.CommitAsync();
            _channel.BasicAck(message.DeliveryTag, multiple: false);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            _channel.BasicNack(message.DeliveryTag, multiple: false, requeue: false);
        }
    }
}
