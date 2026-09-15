namespace transactionalsystem.Domain.Entities;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;
    public long Version { get; protected set; }
}

public abstract class ValueObject
{
}

public interface IRepository<TEntity, TId>
{
    Task<TEntity?> GetByIdAsync(TId id);
    Task AddAsync(TEntity entity);
}

public class PlataformaTransaccionalDistribuidaEventDriven : AggregateRoot<Guid>
{
    public string ReferenceCode { get; private set; } = string.Empty;

    public PlataformaTransaccionalDistribuidaEventDriven(string referenceCode)
    {
        Id = Guid.NewGuid();
        ReferenceCode = referenceCode;
    }
}
