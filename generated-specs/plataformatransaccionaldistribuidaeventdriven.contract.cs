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

    public override boolean Equals(object? obj)
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
public record aOrderCreatedeventispublishedwithauniqueCorrelationIdEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(aOrderCreatedeventispublishedwithauniqueCorrelationIdEvent); }

public record anexistingwebhookeventwithMessageIdABC123Event(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(anexistingwebhookeventwithMessageIdABC123Event); }

public record thesystemreceivesaduplicatewebhookeventwithMessageIdABC123Event(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(thesystemreceivesaduplicatewebhookeventwithMessageIdABC123Event); }

public record thesystemignorestheduplicateeventEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(thesystemignorestheduplicateeventEvent); }

public record anOutboxeventissafelypersistedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(anOutboxeventissafelypersistedEvent); }

public record theCustomerUpdatedeventispublishedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid AggregateId,
    string Details
) : IDomainEvent { public string EventType => nameof(theCustomerUpdatedeventispublishedEvent); }

// --------------------------------------------------------------------------
// 3. Strongly-Typed Command & Query Records
// --------------------------------------------------------------------------
public record CreatePlataformaTransaccionalDistribuidaEventDrivenCommand(
    Guid RequestId,
    Guid TenantId,
    DateTime Timestamp,
    string ReferenceCode,
    decimal Amount
);

public record GetPlataformaTransaccionalDistribuidaEventDrivenQuery(
    Guid PlataformaTransaccionalDistribuidaEventDrivenId,
    Guid TenantId
);

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
