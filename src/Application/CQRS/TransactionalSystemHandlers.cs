// --------------------------------------------------------------------------
// MediatR CQRS Handlers & Read Model Event Projectors
// --------------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using transactionalsystem.Infrastructure.Data;

namespace transactionalsystem.Application.Handlers
{
    // 1. Command Handler
    public class CreateTransactionalSystemCommandHandler : IRequestHandler<CreateTransactionalSystemCommand, CommandResult>
    {
        private readonly ILogger<CreateTransactionalSystemCommandHandler> _logger;
        private readonly ApplicationDbContext _dbContext;

        public CreateTransactionalSystemCommandHandler(ILogger<CreateTransactionalSystemCommandHandler> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<CommandResult> Handle(CreateTransactionalSystemCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing command to create {Feature} with ref {Ref}", "TransactionalSystem", request.ReferenceCode);
            
            // Scaffold: Replace with actual domain logic
            var entityId = Guid.NewGuid();
            
            // Atomic save through DbContext (enables Outbox pattern)
            // _dbContext.TransactionalSystems.Add(newEntity);
            // await _dbContext.SaveChangesAsync(cancellationToken);

            return new CommandResult(true, "TransactionalSystem created successfully (Scaffold)", entityId);
        }
    }

    // 2. Query Handler (Read Model)

    public class GetTransactionalSystemQueryHandler : IRequestHandler<GetTransactionalSystemQuery, TransactionalSystemReadModel?>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetTransactionalSystemQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<TransactionalSystemReadModel?> Handle(GetTransactionalSystemQuery request, CancellationToken cancellationToken)
        {
            // Scaffold: Read from actual Read Model store or DbContext
            // var entity = await _dbContext.TransactionalSystems.FindAsync(request.TransactionalSystemId);
            throw new Exception("Scaffold: Implement database read logic here");
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
