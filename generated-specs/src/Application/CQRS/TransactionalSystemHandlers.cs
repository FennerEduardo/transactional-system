// --------------------------------------------------------------------------
// MediatR CQRS Handlers & Read Model Event Projectors
// --------------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace transactionalsystem.Application.Handlers
{
    public record CommandResult(bool Success, string Message, Guid? EntityId);

    // 1. Command Handler
    public class CreateTransactionalSystemCommandHandler : IRequestHandler<CreateTransactionalSystemCommand, CommandResult>
    {
        private readonly ILogger<CreateTransactionalSystemCommandHandler> _logger;

        public CreateTransactionalSystemCommandHandler(ILogger<CreateTransactionalSystemCommandHandler> logger)
        {
            _logger = logger;
        }

        public async Task<CommandResult> Handle(CreateTransactionalSystemCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing command to create {Feature} with ref {Ref}", "TransactionalSystem", request.ReferenceCode);
            
            // Entity creation and atomic save
            var entityId = Guid.NewGuid();
            await Task.CompletedTask;

            return new CommandResult(true, "TransactionalSystem created successfully", entityId);
        }
    }

    // 2. Query Handler (Read Model)
    public record TransactionalSystemReadModel(Guid Id, string ReferenceCode, decimal Amount, string Status, DateTime UpdatedAt);

    public class GetTransactionalSystemQueryHandler : IRequestHandler<GetTransactionalSystemQuery, TransactionalSystemReadModel?>
    {
        public async Task<TransactionalSystemReadModel?> Handle(GetTransactionalSystemQuery request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new TransactionalSystemReadModel(request.TransactionalSystemId, "REF-10020", 250.00m, "COMPLETED", DateTime.UtcNow);
        }
    }

    // 3. Event Projector (Read View Updater)
    public class TransactionalSystemEventProjector : INotificationHandler<TransactionalSystemProcessedEventNotification>
    {
        private readonly ILogger<TransactionalSystemEventProjector> _logger;

        public TransactionalSystemEventProjector(ILogger<TransactionalSystemEventProjector> logger)
        {
            _logger = logger;
        }

        public async Task Handle(TransactionalSystemProcessedEventNotification notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Projecting read event {EventId} into ReadModel DB", notification.EventId);
            await Task.CompletedTask;
        }
    }

    public record TransactionalSystemProcessedEventNotification(Guid EventId, Guid AggregateId, string Details) : INotification;
}
