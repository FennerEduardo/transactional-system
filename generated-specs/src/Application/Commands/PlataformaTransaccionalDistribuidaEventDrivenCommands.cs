namespace transactionalsystem.Application.Commands;

using System;
using MediatR;

public record CreatePlataformaTransaccionalDistribuidaEventDrivenCommand(string ReferenceCode, decimal Amount) : IRequest<string>;
public record GetPlataformaTransaccionalDistribuidaEventDrivenQuery(Guid PlataformaTransaccionalDistribuidaEventDrivenId) : IRequest<object?>;
