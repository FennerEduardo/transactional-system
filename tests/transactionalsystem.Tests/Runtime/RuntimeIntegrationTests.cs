using System.Diagnostics;
using System.Text;
using transactionalsystem.Domain.Kernel;
using transactionalsystem.Runtime;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Xunit;

namespace transactionalsystem.Tests.Runtime;

/// <summary>Runtime integration tests (docs/RUNTIME-KERNEL.md, IT1-IT7). Requires DATABASE_URL and AMQP_URL.</summary>
[Trait("Category", "Integration")]
[Collection("runtime")]
public sealed class RuntimeIntegrationTests : IAsyncLifetime
{
    private const string Command = "process_transactional_system";
    private const string Event = "TransactionalSystemProcessed";
    private readonly NpgsqlDataSource _db;
    private readonly IConnection _amqp;
    private readonly string _schema;
    private readonly Topology _topology;

    public RuntimeIntegrationTests()
    {
        var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var amqpUrl = Environment.GetEnvironmentVariable("AMQP_URL");
        if (string.IsNullOrEmpty(dbUrl) || string.IsNullOrEmpty(amqpUrl))
            throw new InvalidOperationException("Integration tests need DATABASE_URL and AMQP_URL (see docs/RUNTIME-KERNEL.md).");
        _db = Connections.FromUrl(dbUrl);
        _amqp = new ConnectionFactory { Uri = new Uri(amqpUrl) }.CreateConnection();
        var uid = Guid.NewGuid().ToString("N")[..12];
        _schema = $"it_{uid}";
        _topology = Topology.ForPrefix($"it-{uid}");
    }

    public Task InitializeAsync() => Schema.MigrateAsync(_db, _schema);

    public async Task DisposeAsync()
    {
        await using (var cmd = _db.CreateCommand($"DROP SCHEMA IF EXISTS {_schema} CASCADE")) await cmd.ExecuteNonQueryAsync();
        _amqp.Dispose();
        await _db.DisposeAsync();
    }

    private async Task<long> Count(string table, string where = "true", params object[] args)
    {
        await using var cmd = _db.CreateCommand($"SELECT count(*) FROM {_schema}.{table} WHERE {where}");
        foreach (var a in args) cmd.Parameters.Add(new NpgsqlParameter { Value = a });
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static List<BasicGetResult> DrainQueue(IModel channel, string queue)
    {
        var messages = new List<BasicGetResult>();
        for (var m = channel.BasicGet(queue, autoAck: true); m is not null; m = channel.BasicGet(queue, autoAck: true)) messages.Add(m);
        return messages;
    }

    private static async Task WaitFor(Func<bool> check)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!check())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Timed out waiting for condition");
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task IT1_AtomicWriteAndRollback()
    {
        var service = new CommandService(_db, _schema);
        var result = await service.HandleAsync(new("t1", "agg-1", Command));
        Assert.Equal(("created", Event, 1L), (result.Status, result.EventType, result.Version!.Value));
        Assert.Equal(1, (await service.LoadAsync("t1", "agg-1"))!.Version);
        Assert.Equal(1, await Count("ghk_outbox", "aggregate_id = $1", "agg-1"));

        var failure = await Assert.ThrowsAsync<DomainValidationException>(() => service.HandleAsync(new("t1", "agg-2", "no_such_command", IdempotencyKey: "k-fail")));
        Assert.Contains("Unknown command", failure.Message);
        Assert.Null(await service.LoadAsync("t1", "agg-2"));
        Assert.Equal(0, await Count("ghk_outbox", "aggregate_id = $1", "agg-2"));
        Assert.Equal(0, await Count("ghk_idempotency", "key = $1", "k-fail"));
    }

    [Fact]
    public async Task IT2_ConcurrentIdempotentRequests()
    {
        var service = new CommandService(_db, _schema);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => service.HandleAsync(new("t1", "agg-1", Command, IdempotencyKey: "key-1")))));
        Assert.Equal(1, await Count("ghk_outbox"));
        Assert.Equal(1, (await service.LoadAsync("t1", "agg-1"))!.Version);
        Assert.Single(results, r => r.Status == "created");
        Assert.All(results, r => Assert.Equal((Event, 1L), (r.EventType, r.Version!.Value)));
    }

    [Fact]
    public async Task IT3_ConcurrentRelaysPublishExactlyOnce()
    {
        var service = new CommandService(_db, _schema);
        for (var i = 0; i < 20; i++) await service.HandleAsync(new("t1", $"agg-{i}", Command));
        using var setup = _amqp.CreateModel();
        _topology.Declare(setup);
        async Task<int> Drain(string worker)
        {
            using var channel = _amqp.CreateModel();
            var relay = new OutboxRelay(_db, channel, _topology.Exchange, _schema);
            var total = 0;
            for (var n = await relay.PublishBatchAsync(worker, 3); n > 0; n = await relay.PublishBatchAsync(worker, 3)) total += n;
            return total;
        }
        var totals = await Task.WhenAll(Task.Run(() => Drain("relay-a")), Task.Run(() => Drain("relay-b")));
        Assert.Equal(20, totals.Sum());
        Assert.Equal(0, await Count("ghk_outbox", "published_at IS NULL"));
        await WaitFor(() => setup.QueueDeclarePassive(_topology.Queue).MessageCount == 20);
        Assert.Equal(20, DrainQueue(setup, _topology.Queue).Select(m => m.BasicProperties.MessageId).Distinct().Count());
    }

    [Fact]
    public async Task IT4_TenantIsolation()
    {
        var service = new CommandService(_db, _schema);
        await service.HandleAsync(new("tenant-a", "shared-id", Command));
        Assert.Null(await service.LoadAsync("tenant-b", "shared-id"));
        await service.HandleAsync(new("tenant-b", "shared-id", Command));
        Assert.Equal(1, (await service.LoadAsync("tenant-a", "shared-id"))!.Version);
        Assert.Equal(1, (await service.LoadAsync("tenant-b", "shared-id"))!.Version);
        Assert.Equal(1, await Count("ghk_outbox", "tenant_id = $1", "tenant-a"));
    }

    [Fact]
    public async Task IT5_SagaCompensatesInReverseOrder()
    {
        var saga = new SagaOrchestrator(_db, _schema);
        var log = new List<string>();
        SagaOrchestrator.Step Step(string name, bool fail = false) => new(name,
            () => { if (fail) throw new InvalidOperationException($"{name} failed"); log.Add($"do:{name}"); return Task.CompletedTask; },
            () => { log.Add($"undo:{name}"); return Task.CompletedTask; });
        Assert.Equal("COMPENSATED", await saga.RunAsync("saga-1", "t1", new[] { Step("reserve"), Step("charge"), Step("ship", fail: true) }));
        Assert.Equal(new[] { "do:reserve", "do:charge", "undo:charge", "undo:reserve" }, log);
        var status = await saga.StatusAsync("saga-1");
        Assert.Equal("COMPENSATED", status!.Value);
        Assert.Empty(status.CompletedSteps);
        Assert.Equal("COMPLETED", await saga.RunAsync("saga-2", "t1", new[] { Step("reserve"), Step("charge") }));
    }

    [Fact]
    public async Task IT6_InboxDeduplicatesAndDeadLetters()
    {
        using var channel = _amqp.CreateModel();
        _topology.Declare(channel);
        var handled = new List<string>();
        var consumer = new InboxConsumer(_db, channel, _topology.Queue, "it-consumer", (json, meta) =>
        {
            if (json.TryGetProperty("poison", out _)) throw new InvalidOperationException("cannot process");
            handled.Add(meta.MessageId);
            return Task.CompletedTask;
        }, _schema);
        foreach (var (id, body) in new[] { ("m-1", "{\"ok\": true}"), ("m-1", "{\"ok\": true}"), ("m-poison", "{\"poison\": true}") })
        {
            var props = channel.CreateBasicProperties();
            props.MessageId = id;
            props.Headers = new Dictionary<string, object> { ["tenant_id"] = "t1" };
            channel.BasicPublish(_topology.Exchange, "Test", props, Encoding.UTF8.GetBytes(body));
        }
        await consumer.DrainAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { "m-1" }, handled);
        Assert.Equal(1, await Count("ghk_inbox", "consumer = $1", "it-consumer"));
        await WaitFor(() => channel.QueueDeclarePassive(_topology.Dlq).MessageCount == 1);
        Assert.Equal(new[] { "m-poison" }, DrainQueue(channel, _topology.Dlq).Select(m => m.BasicProperties.MessageId));
    }

    [Fact]
    public async Task IT7_TraceContextPropagation()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource(Telemetry.Source.Name).AddInMemoryExporter(exported).Build();
        var service = new CommandService(_db, _schema);
        await service.HandleAsync(new("t1", "agg-1", Command, Traceparent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"));
        await using (var cmd = _db.CreateCommand($"SELECT traceparent FROM {_schema}.ghk_outbox"))
            Assert.Contains("4bf92f3577b34da6a3ce929d0e0e4736", (string)(await cmd.ExecuteScalarAsync())!);
        using var channel = _amqp.CreateModel();
        _topology.Declare(channel);
        Assert.Equal(1, await new OutboxRelay(_db, channel, _topology.Exchange, _schema).PublishBatchAsync("relay", 10));
        var received = 0;
        using var consumerChannel = _amqp.CreateModel();
        await new InboxConsumer(_db, consumerChannel, _topology.Queue, "trace-consumer", (_, _) => { received++; return Task.CompletedTask; }, _schema).DrainAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, received);
        provider!.ForceFlush();

        var command = exported.Single(a => a.Kind == ActivityKind.Internal);
        var producer = exported.Single(a => a.Kind == ActivityKind.Producer);
        var consumer = exported.Single(a => a.Kind == ActivityKind.Consumer);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", command.TraceId.ToHexString());
        Assert.Equal("00f067aa0ba902b7", command.ParentSpanId.ToHexString());
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", producer.TraceId.ToHexString());
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", consumer.TraceId.ToHexString());
        Assert.Equal(producer.SpanId, consumer.ParentSpanId);
    }
}
