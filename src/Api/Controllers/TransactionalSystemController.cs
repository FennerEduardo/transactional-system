namespace transactionalsystem.Api.Controllers;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using transactionalsystem.Application.Commands;

[ApiController]
[Route("api/v1/[controller]")]
public class TransactionalSystemController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionalSystemController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionalSystemCommand command)
    {
        var result = await _mediator.Send(command);
        return Accepted(result);
    }
}
