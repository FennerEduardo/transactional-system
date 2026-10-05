using System.Diagnostics;
using System.Text;
using Npgsql;
using RabbitMQ.Client;

namespace transactionalsystem.Runtime;

/// <summary>
/// Publishes pending outbox rows. Rows are claimed with FOR UPDATE SKIP LOCKED and a lease, so
/// concurrent relays never publish the same row; publisher confirms guarantee delivery to the broker
/// before a row is marked published. Use one relay (and channel) per thread.
/// </summary>
public sealed class OutboxRelay
{
    private readonly NpgsqlDataSource _db;
    private readonly IModel _channel;
    private readonly string _exchange;
    private readonly string _s;
    public int LeaseSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 5;

    public OutboxRelay(NpgsqlDataSource db, IModel channel, string exchange, string? schema = null)
    {
        _db = db;
        _channel = channel;
        _exchange = exchange;
        _s = Schema.Name(schema);
        _channel.ConfirmSelect();
    }

    private sealed record Claimed(string Id, string TenantId, string EventType, string Payload, string? Traceparent);

    public async Task<int> PublishBatchAsync(string workerId, int limit = 50, CancellationToken ct = default)
    {
        var batch = new List<Claimed>();
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using (var claim = new NpgsqlCommand(
            $"UPDATE {_s}.ghk_outbox SET claimed_by = $1, claimed_until = now() + make_interval(secs => $2), attempts = attempts + 1 " +
            $"WHERE id IN (SELECT id FROM {_s}.ghk_outbox WHERE published_at IS NULL AND failed_at IS NULL AND (claimed_until IS NULL OR claimed_until < now()) " +
            "ORDER BY created_at LIMIT $3 FOR UPDATE SKIP LOCKED) RETURNING id, tenant_id, event_type, payload, traceparent", conn)
        { Parameters = { new() { Value = workerId }, new() { Value = (double)LeaseSeconds }, new() { Value = limit } } })
        await using (var reader = await claim.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                batch.Add(new Claimed(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        var published = 0;
        foreach (var row in batch)
        {
            using var activity = Telemetry.Source.StartActivity($"{_exchange} publish", ActivityKind.Producer, Telemetry.ParentFrom(row.Traceparent));
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", _exchange);
            activity?.SetTag("messaging.message.id", row.Id);
            activity?.SetTag("tenant.id", row.TenantId);
            try
            {
                var props = _channel.CreateBasicProperties();
                props.MessageId = row.Id;
                props.Persistent = true;
                props.ContentType = "application/json";
                props.Type = row.EventType;
                props.Headers = new Dictionary<string, object> { ["tenant_id"] = row.TenantId };
                if (Telemetry.TraceparentOf(activity) is { } traceparent) props.Headers["traceparent"] = traceparent;
                _channel.BasicPublish(_exchange, row.EventType, props, Encoding.UTF8.GetBytes(row.Payload));
                _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
                await CommandService.Exec(conn, $"UPDATE {_s}.ghk_outbox SET published_at = now(), claimed_until = NULL WHERE id = $1 AND claimed_by = $2", ct, row.Id, workerId);
                published++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                await CommandService.Exec(conn, $"UPDATE {_s}.ghk_outbox SET claimed_until = NULL, last_error = $2, failed_at = CASE WHEN attempts >= $3 THEN now() ELSE NULL END WHERE id = $1",
                    ct, row.Id, e.Message, MaxAttempts);
            }
        }
        return published;
    }
}
