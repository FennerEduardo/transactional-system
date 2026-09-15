namespace transactionalsystem.Infrastructure.Repositories;

using System;
using System.Threading.Tasks;
using transactionalsystem.Domain.Entities;

public class PlataformaTransaccionalDistribuidaEventDrivenRepository : IRepository<PlataformaTransaccionalDistribuidaEventDriven, Guid>
{
    public async Task<PlataformaTransaccionalDistribuidaEventDriven?> GetByIdAsync(Guid id)
    {
        await Task.CompletedTask;
        return new PlataformaTransaccionalDistribuidaEventDriven("REF-SAMPLE");
    }

    public async Task AddAsync(PlataformaTransaccionalDistribuidaEventDriven entity)
    {
        await Task.CompletedTask;
    }
}
