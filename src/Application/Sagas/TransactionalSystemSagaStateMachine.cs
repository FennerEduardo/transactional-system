// --------------------------------------------------------------------------
// Saga Orchestration Pattern (.NET 8/9)
// --------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using MassTransit;

namespace transactionalsystem.Application.Sagas
{
    public class TransactionalSystemSagaState : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; } = string.Empty;
        public Guid TransactionalSystemId { get; set; }
        public string ErrorReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? TimeoutDeadline { get; set; }
    }

    public class TransactionalSystemSagaStateMachine : MassTransitStateMachine<TransactionalSystemSagaState>
    {
        public State Authorized { get; private set; } = null!;
        public State Completed { get; private set; } = null!;
        public State Compensating { get; private set; } = null!;
        public State Failed { get; private set; } = null!;

        public Event<TransactionalSystemInitiatedEvent> TransactionalSystemInitiated { get; private set; } = null!;
        public Event<TransactionalSystemAuthorizedEvent> TransactionalSystemAuthorized { get; private set; } = null!;
        public Event<TransactionalSystemFailedEvent> TransactionalSystemFailed { get; private set; } = null!;
        public Event<SagaTimeoutExpiredEvent> TimeoutExpired { get; private set; } = null!;

        public TransactionalSystemSagaStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => TransactionalSystemInitiated, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => TransactionalSystemAuthorized, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => TransactionalSystemFailed, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => TimeoutExpired, x => x.CorrelateById(m => m.Message.CorrelationId));

            Initially(
                When(TransactionalSystemInitiated)
                    .Then(context => {
                        context.Saga.TransactionalSystemId = context.Message.TransactionalSystemId;
                        context.Saga.CreatedAt = DateTime.UtcNow;
                        context.Saga.TimeoutDeadline = DateTime.UtcNow.AddMinutes(5);
                    })
                    .Publish(context => new AuthorizeTransactionalSystemCommand { TransactionalSystemId = context.Saga.TransactionalSystemId })
                    .TransitionTo(Authorized)
            );

            During(Authorized,
                When(TransactionalSystemAuthorized)
                    .Publish(context => new CompleteTransactionalSystemCommand { TransactionalSystemId = context.Saga.TransactionalSystemId })
                    .Then(context => context.Saga.UpdatedAt = DateTime.UtcNow)
                    .TransitionTo(Completed),
                When(TransactionalSystemFailed)
                    .TransitionTo(Compensating)
                    .Then(context => {
                        context.Saga.ErrorReason = context.Message.Reason;
                    })
                    .Publish(context => new CompensateTransactionalSystemCommand { TransactionalSystemId = context.Saga.TransactionalSystemId, Reason = context.Message.Reason })
                    .TransitionTo(Failed),
                When(TimeoutExpired)
                    .TransitionTo(Compensating)
                    .Then(context => {
                        context.Saga.ErrorReason = "Saga timeout reached without authorization.";
                    })
                    .Publish(context => new CompensateTransactionalSystemCommand { TransactionalSystemId = context.Saga.TransactionalSystemId, Reason = "Timeout" })
                    .TransitionTo(Failed)
            );
        }
    }

    public record TransactionalSystemInitiatedEvent(Guid CorrelationId, Guid TransactionalSystemId);
    public record TransactionalSystemAuthorizedEvent(Guid CorrelationId);
    public record TransactionalSystemFailedEvent(Guid CorrelationId, string Reason);
    public record SagaTimeoutExpiredEvent(Guid CorrelationId);
    public record AuthorizeTransactionalSystemCommand { public Guid TransactionalSystemId { get; init; } }
    public record CompleteTransactionalSystemCommand { public Guid TransactionalSystemId { get; init; } }
    public record CompensateTransactionalSystemCommand { public Guid TransactionalSystemId { get; init; } public string Reason { get; init; } = string.Empty; }
}
