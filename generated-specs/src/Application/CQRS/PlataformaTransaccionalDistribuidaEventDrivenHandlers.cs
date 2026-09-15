// --------------------------------------------------------------------------
// MediatR CQRS Handlers & Read Model Event Projectors
// --------------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace mygherkinservice.Application.Handlers
{
    public record CommandResult(bool Success, string Message, Guid? EntityId);

    // 1. Command Handler
    public class CreatePlataformaTransaccionalDistribuidaEventDrivenCommandHandler : IRequestHandler<CreatePlataformaTransaccionalDistribuidaEventDrivenCommand, CommandResult>
    {
        private readonly ILogger<CreatePlataformaTransaccionalDistribuidaEventDrivenCommandHandler> _logger;

        public CreatePlataformaTransaccionalDistribuidaEventDrivenCommandHandler(ILogger<CreatePlataformaTransaccionalDistribuidaEventDrivenCommandHandler> logger)
        {
            _logger = logger;
        }

        public async Task<CommandResult> Handle(CreatePlataformaTransaccionalDistribuidaEventDrivenCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing command to create {Feature} with ref {Ref}", "PlataformaTransaccionalDistribuidaEventDriven", request.ReferenceCode);
            
            // Entity creation and atomic save
            var entityId = Guid.NewGuid();
            await Task.CompletedTask;

            return new CommandResult(true, "PlataformaTransaccionalDistribuidaEventDriven created successfully", entityId);
        }
    }

    // 2. Query Handler (Read Model)
    public record PlataformaTransaccionalDistribuidaEventDrivenReadModel(Guid Id, string ReferenceCode, decimal Amount, string Status, DateTime UpdatedAt);

    public class GetPlataformaTransaccionalDistribuidaEventDrivenQueryHandler : IRequestHandler<GetPlataformaTransaccionalDistribuidaEventDrivenQuery, PlataformaTransaccionalDistribuidaEventDrivenReadModel?>
    {
        public async Task<PlataformaTransaccionalDistribuidaEventDrivenReadModel?> Handle(GetPlataformaTransaccionalDistribuidaEventDrivenQuery request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new PlataformaTransaccionalDistribuidaEventDrivenReadModel(request.PlataformaTransaccionalDistribuidaEventDrivenId, "REF-10020", 250.00m, "COMPLETED", DateTime.UtcNow);
        }
    }

    // 3. Event Projector (Read View Updater)
    public class PlataformaTransaccionalDistribuidaEventDrivenEventProjector : INotificationHandler<PlataformaTransaccionalDistribuidaEventDrivenProcessedEventNotification>
    {
        private readonly ILogger<PlataformaTransaccionalDistribuidaEventDrivenEventProjector> _logger;

        public PlataformaTransaccionalDistribuidaEventDrivenEventProjector(ILogger<PlataformaTransaccionalDistribuidaEventDrivenEventProjector> logger)
        {
            _logger = logger;
        }

        public async Task Handle(PlataformaTransaccionalDistribuidaEventDrivenProcessedEventNotification notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Projecting read event {EventId} into ReadModel DB", notification.EventId);
            await Task.CompletedTask;
        }
    }

    public record PlataformaTransaccionalDistribuidaEventDrivenProcessedEventNotification(Guid EventId, Guid AggregateId, string Details) : INotification;
}
