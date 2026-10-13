using transactionalsystem.Runtime;
using Npgsql;
using RabbitMQ.Client;

namespace transactionalsystem.Api.Workers;

/// <summary>Publishes the outbox every 1000 ms when AMQP_URL is set.</summary>
public sealed class OutboxRelayWorker(NpgsqlDataSource db, IConfiguration config, ILogger<OutboxRelayWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"transactional-system-{Guid.NewGuid()}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Outbox:RelayEnabled", true)) return;
        var url = config["AMQP_URL"];
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("AMQP_URL is not set: outbox events are stored but not published.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = new ConnectionFactory { Uri = new Uri(url) }.CreateConnection();
                using var channel = connection.CreateModel();
                Topology.Default.Declare(channel);
                var relay = new OutboxRelay(db, channel, Topology.Default.Exchange);
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
