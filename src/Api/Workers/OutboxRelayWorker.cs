using transactionalsystem.Runtime;
using Npgsql;

namespace transactionalsystem.Api.Workers;

/// <summary>
/// Publishes the outbox every 1000 ms when a broker is configured (Runtime/Brokers.cs: GHK_BROKER,
/// AMQP_URL, PUBSUB_*, SERVICEBUS_*).
/// </summary>
public sealed class OutboxRelayWorker(NpgsqlDataSource db, IConfiguration config, ILogger<OutboxRelayWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"transactional-system-{Guid.NewGuid()}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Outbox:RelayEnabled", true)) return;
        if (!Brokers.Configured())
        {
            logger.LogWarning("No event broker is configured (AMQP_URL, PUBSUB_PROJECT_ID or SERVICEBUS_*): outbox events are stored but not published.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var broker = await Brokers.ConnectAsync();
                // GHK_DECLARE_TOPOLOGY=false when infrastructure as code owns the topology (least-privilege credentials).
                if (Environment.GetEnvironmentVariable("GHK_DECLARE_TOPOLOGY") != "false")
                    await broker.DeclareAsync(Topology.Default, stoppingToken);
                await using var publisher = await broker.PublisherAsync(Topology.Default.Exchange, stoppingToken);
                var relay = new OutboxRelay(db, publisher);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await relay.PublishBatchAsync(_workerId, 50, stoppingToken);
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Outbox relay failed; retrying");
                await Task.Delay(5000, stoppingToken).ContinueWith(_ => { });
            }
        }
    }
}
