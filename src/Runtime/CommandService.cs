using System.Diagnostics;
using System.Text.Json;
using transactionalsystem.Domain.Kernel;
using Npgsql;

namespace transactionalsystem.Runtime;

/// <summary>
/// Executes a command in one transaction: idempotency claim, aggregate load (FOR UPDATE), domain
/// logic, aggregate save and outbox insert. A domain error rolls back everything.
/// </summary>
public sealed class CommandService
{
    /// <summary>Aggregate type used when a request does not name one.</summary>
    public const string DefaultAggregate = "TransactionalSystem";

    /// <summary>The new state of an aggregate after a command, and the recorded event.</summary>
    private sealed record Applied(string State, long Version, string EventType, long EventVersion, object Event);

    /// <summary>Every aggregate of this service: (id, state?, version, command, payload) -> applied command.</summary>
    private static readonly Dictionary<string, Func<string, string?, long, string, IReadOnlyDictionary<string, object?>?, Applied>> Aggregates = new()
    {
        ["TransactionalSystem"] = (id, state, version, command, payload) =>
        {
            var aggregate = state is null ? new TransactionalSystemAggregate(id) : TransactionalSystemAggregate.Restore(id, state, version);
            var cmd = new TransactionalSystemCommand(id, payload);
            TransactionalSystemDomainEvent @event = command switch
            {
                "process_transactional_system" => aggregate.ProcessTransactionalSystem(cmd),
                _ => throw new DomainValidationException($"Unknown command {command}")
            };
            return new Applied(aggregate.State, aggregate.Version, @event.Type, @event.Version, @event);
        },
    };

    /// <summary>AggregateType: the feature's aggregate (e.g. TransactionalSystem); null means DefaultAggregate.</summary>
    public sealed record Request(string TenantId, string AggregateId, string Command, IReadOnlyDictionary<string, object?>? Payload = null, string? IdempotencyKey = null, string? Traceparent = null, string? AggregateType = null)
    {
        public string Type => string.IsNullOrWhiteSpace(AggregateType) ? DefaultAggregate : AggregateType;
    }

    public sealed record Result(string Status, string AggregateId, string? EventType = null, long? Version = null);

    public sealed record Snapshot(string State, long Version);

    private readonly NpgsqlDataSource _db;
    private readonly string _s;

    public CommandService(NpgsqlDataSource db, string? schema = null)
    {
        _db = db;
        _s = Schema.Name(schema);
    }

    public async Task<Result> HandleAsync(Request req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.TenantId)) throw new DomainValidationException("TenantId is required");
        using var activity = Telemetry.Source.StartActivity($"{req.Type}.{req.Command}", ActivityKind.Internal, Telemetry.ParentFrom(req.Traceparent));
        activity?.SetTag("tenant.id", req.TenantId);
        activity?.SetTag("aggregate.id", req.AggregateId);
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var result = await ExecuteAsync(conn, req, Telemetry.TraceparentOf(activity), ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception e)
        {
            await tx.RollbackAsync(CancellationToken.None);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            throw;
        }
    }

    private async Task<Result> ExecuteAsync(NpgsqlConnection conn, Request req, string? traceparent, CancellationToken ct)
    {
        if (req.IdempotencyKey is { } key)
        {
            var claimed = await Exec(conn, $"INSERT INTO {_s}.ghk_idempotency (tenant_id, key, status) VALUES ($1, $2, 'PROCESSING') ON CONFLICT DO NOTHING", ct, req.TenantId, key);
            if (claimed == 0)
            {
                await using var read = new NpgsqlCommand($"SELECT status, response FROM {_s}.ghk_idempotency WHERE tenant_id = $1 AND key = $2", conn) { Parameters = { new() { Value = req.TenantId }, new() { Value = key } } };
                await using var reader = await read.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct) && reader.GetString(0) == "COMPLETED")
                {
                    var stored = JsonSerializer.Deserialize<Result>(reader.GetString(1))!;
                    return stored with { Status = "replayed" };
                }
                return new Result("in-progress", req.AggregateId);
            }
        }

        if (!Aggregates.TryGetValue(req.Type, out var apply)) throw new DomainValidationException($"Unknown aggregate type {req.Type}");
        string? state = null;
        long version = 0;
        await using (var load = new NpgsqlCommand($"SELECT state, version FROM {_s}.ghk_aggregates WHERE tenant_id = $1 AND aggregate_type = $2 AND id = $3 FOR UPDATE", conn)
        { Parameters = { new() { Value = req.TenantId }, new() { Value = req.Type }, new() { Value = req.AggregateId } } })
        await using (var reader = await load.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                state = reader.GetString(0);
                version = reader.GetInt32(1);
            }
        }
        var applied = apply(req.AggregateId, state, version, req.Command, req.Payload);

        await Exec(conn, $"INSERT INTO {_s}.ghk_aggregates (tenant_id, aggregate_type, id, state, version) VALUES ($1, $2, $3, $4, $5) " +
            "ON CONFLICT (tenant_id, aggregate_type, id) DO UPDATE SET state = EXCLUDED.state, version = EXCLUDED.version, updated_at = now()",
            ct, req.TenantId, req.Type, req.AggregateId, applied.State, (int)applied.Version);
        await Exec(conn, $"INSERT INTO {_s}.ghk_outbox (id, tenant_id, aggregate_id, event_type, payload, traceparent) VALUES ($1, $2, $3, $4, $5, $6)",
            ct, Guid.NewGuid().ToString(), req.TenantId, req.AggregateId, applied.EventType, JsonSerializer.Serialize(applied.Event, applied.Event.GetType()), (object?)traceparent ?? DBNull.Value);
        var result = new Result("created", req.AggregateId, applied.EventType, applied.EventVersion);
        if (req.IdempotencyKey is { } completedKey)
        {
            await Exec(conn, $"UPDATE {_s}.ghk_idempotency SET status = 'COMPLETED', response = $3 WHERE tenant_id = $1 AND key = $2", ct, req.TenantId, completedKey, JsonSerializer.Serialize(result));
        }
        return result;
    }

    /// <summary>The aggregate as the tenant sees it (null for other tenants' aggregates).</summary>
    public async Task<Snapshot?> LoadAsync(string tenantId, string aggregateId, string aggregateType = DefaultAggregate)
    {
        await using var cmd = _db.CreateCommand($"SELECT state, version FROM {_s}.ghk_aggregates WHERE tenant_id = $1 AND aggregate_type = $2 AND id = $3");
        cmd.Parameters.Add(new() { Value = tenantId });
        cmd.Parameters.Add(new() { Value = aggregateType });
        cmd.Parameters.Add(new() { Value = aggregateId });
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new Snapshot(reader.GetString(0), reader.GetInt32(1)) : null;
    }

    internal static async Task<int> Exec(NpgsqlConnection conn, string sql, CancellationToken ct, params object?[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args) cmd.Parameters.Add(new NpgsqlParameter { Value = arg ?? DBNull.Value });
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}
