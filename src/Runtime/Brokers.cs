using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace transactionalsystem.Runtime;

/// <summary>A domain event on the wire, independent of the broker.</summary>
public sealed record BrokerMessage(string MessageId, string? EventType, string? TenantId, string? Traceparent, byte[] Body);

/// <summary>Publishes to one destination; PublishAsync completes only once the broker has durably accepted the message.</summary>
public interface IEventPublisher : IAsyncDisposable
{
    string MessagingSystem { get; }
    string Destination { get; }
    Task PublishAsync(BrokerMessage message, CancellationToken ct = default);
}

/// <summary>A received message: Ack once processed; DeadLetter moves it to the DLQ (it is not redelivered).</summary>
public sealed record Delivery(BrokerMessage Message, Func<Task> Ack, Func<Task> DeadLetter);

/// <summary>Pulls messages from one queue / subscription.</summary>
public interface IEventSubscription : IAsyncDisposable
{
    string MessagingSystem { get; }
    string Source { get; }
    /// <summary>The next waiting message, or null when there is none.</summary>
    Task<Delivery?> PollAsync(CancellationToken ct = default);
}

public interface IBroker : IAsyncDisposable
{
    string MessagingSystem { get; }
    /// <summary>False when topics cannot be created at run time (Service Bus emulator: fixed Config.json).</summary>
    bool DynamicTopology => true;
    /// <summary>Creates the topology if missing (idempotent); in production it usually comes from infrastructure as code.</summary>
    Task DeclareAsync(Topology topology, CancellationToken ct = default);
    Task<IEventPublisher> PublisherAsync(string destination, CancellationToken ct = default);
    /// <param name="topology">where dead-lettered messages go</param>
    Task<IEventSubscription> SubscribeAsync(string source, Topology topology, CancellationToken ct = default);
}

/// <summary>
/// The event broker behind the outbox relay and the inbox consumer, chosen at run time so one image runs on any
/// cloud: GHK_BROKER=rabbitmq (AMQP_URL), pubsub (PUBSUB_PROJECT_ID or GOOGLE_CLOUD_PROJECT; PUBSUB_EMULATOR_HOST)
/// servicebus (SERVICEBUS_NAMESPACE or SERVICEBUS_CONNECTION_STRING) or kafka (KAFKA_BOOTSTRAP_SERVERS), when
/// generated. Without GHK_BROKER the configured variables decide, then stack.messaging.
/// </summary>
public static class Brokers
{
    /// <summary>Broker of the generated configuration (stack.messaging).</summary>
    public const string DefaultKind = "rabbitmq";

    private static string? Env(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : null;

    public static string Kind()
    {
        if (Env("GHK_BROKER") is { } explicitKind) return explicitKind.Trim().ToLowerInvariant();
        if (Env("AMQP_URL") is not null) return "rabbitmq";
        if (Env("PUBSUB_PROJECT_ID") is not null || Env("PUBSUB_EMULATOR_HOST") is not null) return "pubsub";
        if (Env("SERVICEBUS_CONNECTION_STRING") is not null || Env("SERVICEBUS_NAMESPACE") is not null) return "servicebus";
        if (Env("KAFKA_BOOTSTRAP_SERVERS") is not null) return "kafka";
        return DefaultKind;
    }

    /// <summary>Whether the variables of the selected broker are set.</summary>
    public static bool Configured() => Kind() switch
    {
        "pubsub" => Env("PUBSUB_PROJECT_ID") is not null || Env("GOOGLE_CLOUD_PROJECT") is not null,
        "servicebus" => Env("SERVICEBUS_CONNECTION_STRING") is not null || Env("SERVICEBUS_NAMESPACE") is not null,
        "kafka" => Env("KAFKA_BOOTSTRAP_SERVERS") is not null,
        _ => Env("AMQP_URL") is not null
    };

    public static async Task<IBroker> ConnectAsync() => Kind() switch
    {
        "rabbitmq" => (IBroker)await RabbitMqBroker.ConnectAsync(Env("AMQP_URL") ?? throw new InvalidOperationException("AMQP_URL is not set (amqp://user:password@host:5672)")),
        "pubsub" => (IBroker)PubSubBroker.FromEnv(),
        "servicebus" => throw new InvalidOperationException("The Azure Service Bus adapter is not generated: set stack.messaging to \"servicebus\" and regenerate"),
        "kafka" => throw new InvalidOperationException("The Kafka adapter is not generated: set stack.messaging to \"kafka\" and regenerate"),
        var kind => throw new InvalidOperationException($"Unsupported GHK_BROKER: {kind} (rabbitmq, pubsub, servicebus, kafka)")
    };
}

/// <summary>RabbitMQ 0-9-1: topic exchange, publisher confirms, dead-lettering through the queue's DLX.</summary>
public sealed class RabbitMqBroker(IConnection connection) : IBroker
{
    public string MessagingSystem => "rabbitmq";

    public static async Task<RabbitMqBroker> ConnectAsync(string url)
    {
        // A busy host can delay the AMQP handshake: retry before failing.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return new RabbitMqBroker(new ConnectionFactory { Uri = new Uri(url) }.CreateConnection());
            }
            catch (Exception) when (attempt < 5)
            {
                await Task.Delay(2000 * attempt);
            }
        }
    }

    public Task DeclareAsync(Topology t, CancellationToken ct = default)
    {
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(t.Exchange, "topic", durable: true);
        channel.ExchangeDeclare(t.Dlx, "fanout", durable: true);
        channel.QueueDeclare(t.Dlq, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(t.Dlq, t.Dlx, "");
        channel.QueueDeclare(t.Queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object> { ["x-dead-letter-exchange"] = t.Dlx });
        channel.QueueBind(t.Queue, t.Exchange, "#");
        return Task.CompletedTask;
    }

    public Task<IEventPublisher> PublisherAsync(string exchange, CancellationToken ct = default)
    {
        var channel = connection.CreateModel();
        channel.ConfirmSelect();
        return Task.FromResult<IEventPublisher>(new Publisher(channel, exchange));
    }

    public Task<IEventSubscription> SubscribeAsync(string queue, Topology topology, CancellationToken ct = default) =>
        Task.FromResult<IEventSubscription>(new Subscription(connection.CreateModel(), queue));

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Publisher(IModel channel, string exchange) : IEventPublisher
    {
        public string MessagingSystem => "rabbitmq";
        public string Destination => exchange;

        public Task PublishAsync(BrokerMessage m, CancellationToken ct = default)
        {
            var props = channel.CreateBasicProperties();
            props.MessageId = m.MessageId;
            props.Persistent = true;
            props.ContentType = "application/json";
            props.Type = m.EventType;
            props.Headers = new Dictionary<string, object>();
            if (m.TenantId is not null) props.Headers["tenant_id"] = m.TenantId;
            if (m.Traceparent is not null) props.Headers["traceparent"] = m.Traceparent;
            channel.BasicPublish(exchange, m.EventType ?? "", props, m.Body);
            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            channel.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Subscription(IModel channel, string queue) : IEventSubscription
    {
        public string MessagingSystem => "rabbitmq";
        public string Source => queue;

        private static string? Header(IBasicProperties props, string key) =>
            props.Headers is not null && props.Headers.TryGetValue(key, out var value)
                ? value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString()
                : null;

        public Task<Delivery?> PollAsync(CancellationToken ct = default)
        {
            var r = channel.BasicGet(queue, autoAck: false);
            if (r is null) return Task.FromResult<Delivery?>(null);
            var props = r.BasicProperties;
            var message = new BrokerMessage(props.MessageId, props.Type, Header(props, "tenant_id"), Header(props, "traceparent"), r.Body.ToArray());
            // Rejected without requeue: RabbitMQ dead-letters it through the queue's DLX.
            return Task.FromResult<Delivery?>(new Delivery(message,
                () => { channel.BasicAck(r.DeliveryTag, multiple: false); return Task.CompletedTask; },
                () => { channel.BasicNack(r.DeliveryTag, multiple: false, requeue: false); return Task.CompletedTask; }));
        }

        public ValueTask DisposeAsync()
        {
            channel.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Google Cloud Pub/Sub over its REST API (no client library): topics for the exchange and the DLX, pull
/// subscriptions for the queue and the DLQ. PublishAsync completes once Pub/Sub has stored the message. A failed
/// message is dead-lettered by the consumer: published to the DLX topic, then acknowledged.
/// Auth: none against the emulator (PUBSUB_EMULATOR_HOST); otherwise GOOGLE_OAUTH_ACCESS_TOKEN or the
/// metadata-server token of the workload's service account (Cloud Run, GKE workload identity).
/// </summary>
public sealed class PubSubBroker : IBroker
{
    private const string MetadataToken = "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _project;
    private readonly string _root;
    private readonly bool _emulator;
    private string? _token;
    private DateTime _tokenExpiresAt;

    public PubSubBroker(string project, string? emulatorHost)
    {
        _project = project;
        _emulator = !string.IsNullOrWhiteSpace(emulatorHost);
        _root = (_emulator ? $"http://{emulatorHost}" : "https://pubsub.googleapis.com") + $"/v1/projects/{project}";
    }

    public static PubSubBroker FromEnv()
    {
        var project = Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID") is { Length: > 0 } p ? p : Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        if (string.IsNullOrWhiteSpace(project)) throw new InvalidOperationException("PUBSUB_PROJECT_ID (or GOOGLE_CLOUD_PROJECT) is not set");
        return new PubSubBroker(project, Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST"));
    }

    public string MessagingSystem => "gcp_pubsub";

    private async Task<string?> BearerAsync(CancellationToken ct)
    {
        if (_emulator) return null;
        if (Environment.GetEnvironmentVariable("GOOGLE_OAUTH_ACCESS_TOKEN") is { Length: > 0 } fixedToken) return fixedToken;
        if (_token is null || DateTime.UtcNow > _tokenExpiresAt)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, MetadataToken);
            req.Headers.Add("Metadata-Flavor", "Google");
            using var res = await Http.SendAsync(req, ct);
            using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            _token = body.RootElement.GetProperty("access_token").GetString();
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(body.RootElement.GetProperty("expires_in").GetInt32() - 60);
        }
        return _token;
    }

    /// <summary>Calls the API; a status listed in <paramref name="accepted"/> returns null instead of failing.</summary>
    private async Task<JsonElement?> CallAsync(HttpMethod method, string path, object? body, CancellationToken ct, params int[] accepted)
    {
        using var req = new HttpRequestMessage(method, _root + path);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (await BearerAsync(ct) is { } bearer) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var res = await Http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode) return string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
        if (accepted.Contains((int)res.StatusCode)) return null;
        throw new HttpRequestException($"Pub/Sub {method} {path} failed: {(int)res.StatusCode} {text}");
    }

    public async Task DeclareAsync(Topology t, CancellationToken ct = default)
    {
        // 409: it already exists (created earlier or by infrastructure as code).
        await CallAsync(HttpMethod.Put, $"/topics/{t.Exchange}", new { }, ct, 409);
        await CallAsync(HttpMethod.Put, $"/topics/{t.Dlx}", new { }, ct, 409);
        await CallAsync(HttpMethod.Put, $"/subscriptions/{t.Queue}", new { topic = $"projects/{_project}/topics/{t.Exchange}", ackDeadlineSeconds = 60 }, ct, 409);
        await CallAsync(HttpMethod.Put, $"/subscriptions/{t.Dlq}", new { topic = $"projects/{_project}/topics/{t.Dlx}", ackDeadlineSeconds = 60 }, ct, 409);
    }

    private async Task PublishToAsync(string topic, BrokerMessage m, CancellationToken ct)
    {
        var attributes = new Dictionary<string, string> { ["ghk_message_id"] = m.MessageId };
        if (m.EventType is not null) attributes["event_type"] = m.EventType;
        if (m.TenantId is not null) attributes["tenant_id"] = m.TenantId;
        if (m.Traceparent is not null) attributes["traceparent"] = m.Traceparent;
        var res = await CallAsync(HttpMethod.Post, $"/topics/{topic}:publish", new { messages = new[] { new { data = Convert.ToBase64String(m.Body), attributes } } }, ct);
        if (res is not { } r || !r.TryGetProperty("messageIds", out var ids) || ids.GetArrayLength() != 1)
            throw new HttpRequestException($"Pub/Sub did not accept message {m.MessageId}");
    }

    public Task<IEventPublisher> PublisherAsync(string topic, CancellationToken ct = default) => Task.FromResult<IEventPublisher>(new Publisher(this, topic));

    public Task<IEventSubscription> SubscribeAsync(string subscription, Topology topology, CancellationToken ct = default) =>
        Task.FromResult<IEventSubscription>(new Subscription(this, subscription, topology));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class Publisher(PubSubBroker broker, string topic) : IEventPublisher
    {
        public string MessagingSystem => "gcp_pubsub";
        public string Destination => topic;
        public Task PublishAsync(BrokerMessage m, CancellationToken ct = default) => broker.PublishToAsync(topic, m, ct);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Subscription(PubSubBroker broker, string subscription, Topology topology) : IEventSubscription
    {
        public string MessagingSystem => "gcp_pubsub";
        public string Source => subscription;

        private static string? Attr(JsonElement attrs, string key) =>
            attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty(key, out var v) ? v.GetString() : null;

        public async Task<Delivery?> PollAsync(CancellationToken ct = default)
        {
            var res = await broker.CallAsync(HttpMethod.Post, $"/subscriptions/{subscription}:pull", new { maxMessages = 1, returnImmediately = true }, ct);
            if (res is not { } r || !r.TryGetProperty("receivedMessages", out var received) || received.GetArrayLength() == 0) return null;
            var first = received[0];
            var msg = first.GetProperty("message");
            var attrs = msg.TryGetProperty("attributes", out var a) ? a : default;
            var ackId = first.GetProperty("ackId").GetString()!;
            var data = msg.TryGetProperty("data", out var d) ? d.GetString() ?? "" : "";
            var message = new BrokerMessage(Attr(attrs, "ghk_message_id") ?? msg.GetProperty("messageId").GetString()!, Attr(attrs, "event_type"),
                Attr(attrs, "tenant_id"), Attr(attrs, "traceparent"), Convert.FromBase64String(data));
            Func<Task> ack = () => broker.CallAsync(HttpMethod.Post, $"/subscriptions/{subscription}:acknowledge", new { ackIds = new[] { ackId } }, CancellationToken.None);
            return new Delivery(message, ack, async () =>
            {
                await broker.PublishToAsync(topology.Dlx, message, CancellationToken.None);
                await ack();
            });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
