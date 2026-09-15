namespace transactionalsystem.Infrastructure.Repositories;

using System;
using System.Threading.Tasks;
using transactionalsystem.Domain.Entities;

public class TransactionalSystemRepository : IRepository<TransactionalSystem, Guid>
{
    public async Task<TransactionalSystem?> GetByIdAsync(Guid id)
    {
        await Task.CompletedTask;
        return new TransactionalSystem("REF-SAMPLE");
    }

    public async Task AddAsync(TransactionalSystem entity)
    {
        await Task.CompletedTask;
    }
}
