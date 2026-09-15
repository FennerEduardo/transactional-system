/* ==========================================================================
   Generated Strongly-Typed DDD Contracts & Base Classes
   Feature: Plataforma Transaccional Distribuida Event-Driven
   Namespace: transactionalsystem.Domain.PlataformaTransaccionalDistribuidaEventDriven
   ========================================================================== */

namespace transactionalsystem.Domain.PlataformaTransaccionalDistribuidaEventDriven;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

// --------------------------------------------------------------------------
// 1. Core DDD Interfaces & Base Classes
// --------------------------------------------------------------------------
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string EventType { get; }
}

public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;
    public long Version { get; protected set; }
    private readonly List<IDomainEvent> _uncommittedEvents = new();

    public IReadOnlyCollection<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    protected void ApplyChange(IDomainEvent @event)
    {
        _uncommittedEvents.Add(@event);
        Version++;
    }
}

public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType()) return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) => current * 23 + (obj?.GetHashCode() ?? 0));
    }
}

// --------------------------------------------------------------------------
// 2. Strongly-Typed Domain Event Records
// --------------------------------------------------------------------------
public record SystemReceives10IdenticalOrderEventsSameIdempotencyKeyEventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(SystemReceives10IdenticalOrderEventsSameIdempotencyKeyEventEvent); }

public record ProcessingOrderEventsEventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(ProcessingOrderEventsEventEvent); }

public record Ignores9DuplicateEventsIdempotentlyEventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(Ignores9DuplicateEventsIdempotentlyEventEvent); }

public record OrderProcessedEventOnlyOnceEventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(OrderProcessedEventOnlyOnceEventEvent); }

public record ConcurrentEventsAreDistributedAmongInstancesEventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(ConcurrentEventsAreDistributedAmongInstancesEventEvent); }

public record LockingMechanismPreventsRaceConditionsEventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(LockingMechanismPreventsRaceConditionsEventEvent); }

// --------------------------------------------------------------------------
// 3. Strongly-Typed Command & Query Records
// --------------------------------------------------------------------------
public record CommandResult(bool Success, string Message, Guid? EntityId);

public record CreatePlataformaTransaccionalDistribuidaEventDrivenCommand(
    Guid RequestId,
    Guid TenantId,
    DateTime Timestamp,
    string ReferenceCode,
    decimal Amount
) : IRequest<CommandResult>;

public record PlataformaTransaccionalDistribuidaEventDrivenReadModel(Guid Id, string ReferenceCode, decimal Amount, string Status, DateTime UpdatedAt);

public record GetPlataformaTransaccionalDistribuidaEventDrivenQuery(
    Guid PlataformaTransaccionalDistribuidaEventDrivenId,
    Guid TenantId
) : IRequest<PlataformaTransaccionalDistribuidaEventDrivenReadModel?>;

// --------------------------------------------------------------------------
// 4. Strongly-Typed Domain Repository Port
// --------------------------------------------------------------------------
public interface IPlataformaTransaccionalDistribuidaEventDrivenRepository
{
    Task<PlataformaTransaccionalDistribuidaEventDrivenAggregate?> FindByIdAsync(Guid id);
    Task SaveAsync(PlataformaTransaccionalDistribuidaEventDrivenAggregate entity);
    Task DeleteAsync(Guid id);
}

public class PlataformaTransaccionalDistribuidaEventDrivenAggregate : AggregateRoot<Guid>
{
    public string ReferenceCode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    public PlataformaTransaccionalDistribuidaEventDrivenAggregate() { }

    public PlataformaTransaccionalDistribuidaEventDrivenAggregate(Guid id, string referenceCode, decimal amount)
    {
        Id = id;
        ReferenceCode = referenceCode;
        Amount = amount;
        ApplyChange(new PlataformaTransaccionalDistribuidaEventDrivenProcessedEvent(Guid.NewGuid(), DateTime.UtcNow, Id, $"Created PlataformaTransaccionalDistribuidaEventDriven"));
    }
}

// --------------------------------------------------------------------------
// 5. Application Event Publisher Port
// --------------------------------------------------------------------------
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent);
}
