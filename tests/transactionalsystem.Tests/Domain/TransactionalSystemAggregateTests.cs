using transactionalsystem.Domain.Kernel;
using Xunit;

namespace transactionalsystem.Tests.Domain;

public class TransactionalSystemAggregateTests
{
    [Fact]
    public void Starts_in_the_initial_state_with_no_events()
    {
        var aggregate = new TransactionalSystemAggregate("agg-1");
        Assert.Equal(TransactionalSystemStates.PENDING, aggregate.State);
        Assert.Equal(0, aggregate.Version);
        Assert.Empty(aggregate.PendingEvents);
    }

    [Fact]
    public void Rejects_an_aggregate_without_id()
    {
        Assert.Throws<DomainValidationException>(() => new TransactionalSystemAggregate(""));
    }

    [Fact]
    public void ProcessTransactionalSystem_records_TransactionalSystemProcessed_and_bumps_the_version()
    {
        var aggregate = new TransactionalSystemAggregate("agg-1");
        var @event = aggregate.ProcessTransactionalSystem(new TransactionalSystemCommand("agg-1"));
        Assert.Equal(TransactionalSystemEventTypes.TransactionalSystemProcessed, @event.Type);
        Assert.Equal(1, @event.Version);
        Assert.Equal(1, aggregate.Version);
        Assert.Single(aggregate.PendingEvents);
    }

    [Fact]
    public void ProcessTransactionalSystem_rejects_a_command_without_id()
    {
        var aggregate = new TransactionalSystemAggregate("agg-1");
        Assert.Throws<DomainValidationException>(() => aggregate.ProcessTransactionalSystem(new TransactionalSystemCommand("")));
        Assert.Empty(aggregate.PendingEvents);
    }
}
