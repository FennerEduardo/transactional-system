Feature: Plataforma Transaccional Distribuida Event-Driven
  Como operador del sistema
  Quiero una plataforma transaccional distribuida y resiliente
  Para poder procesar pedidos y pagos con garantías de idempotencia, compensación y trazabilidad

  Scenario: AC-01 Duplicate Order (Idempotency in Orders)
    Given the system receives 10 identical order events with the same idempotency key
    When processing the order events
    Then the system stores exactly 1 order record in the database
    And ignores the 9 duplicate events idempotently
    And emits the OrderProcessed event only once

  Scenario: AC-02 Duplicate Payment (Idempotency in Payments)
    Given a payment message is delivered multiple times to the worker
    When the payment worker processes the messages
    Then exactly 1 financial operation is executed in the gateway
    And subsequent duplicate messages are ignored

  Scenario: AC-03 Payment Failure (Saga Compensation)
    Given an order is created and payment is requested
    When the payment gateway returns a failure status
    Then the Saga orchestration triggers a compensation transaction
    And the order status is updated to FAILED

  Scenario: AC-04 Worker Crash (Redelivery and Resilience)
    Given a worker crashes after processing a message but before acknowledging it
    When the message broker redelivers the message to another worker
    Then the idempotency check detects the duplicate
    And no duplicate side effects are executed in the database

  Scenario: AC-05 External Timeout (Circuit Breaker and DLQ)
    Given the payment gateway takes longer than the allowed timeout
    When the system retries the operation multiple times and fails
    Then the circuit breaker transitions to open state
    And the message is routed to the Dead Letter Queue (DLQ) for manual intervention

  Scenario: AC-06 Full Traceability (Correlation ID)
    Given a distributed transaction spanning multiple services
    When the transaction flows through the system
    Then every log and message contains a CorrelationId
    And the operator can reconstruct the entire flow using the CorrelationId

  Scenario: AC-07 Recovery (Saga State Persistence)
    Given a Saga orchestration fails midway due to a system crash
    When the system recovers and restarts
    Then the Saga state machine resumes from its last persisted state
    And it successfully continues or compensates without losing state

  Scenario: AC-08 Horizontal Scaling (No Duplicate Processing)
    Given multiple instances of the worker are running simultaneously
    When concurrent events are distributed among the instances
    Then the locking mechanism prevents race conditions
    And no duplicate operations are executed
