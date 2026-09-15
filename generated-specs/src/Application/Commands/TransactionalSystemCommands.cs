namespace transactionalsystem.Application.Commands;

using System;
using MediatR;

public record CreateTransactionalSystemCommand(string ReferenceCode, decimal Amount) : IRequest<string>;
public record GetTransactionalSystemQuery(Guid TransactionalSystemId) : IRequest<object?>;
