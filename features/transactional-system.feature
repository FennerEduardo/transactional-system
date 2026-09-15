Feature: Plataforma Transaccional Distribuida Event-Driven
  Como plataforma central de e-commerce
  Quiero integrar canales y ejecutar operaciones transaccionales
  Para procesar pedidos, pagos y clientes de forma confiable

  Glossary:
  - COMPLETED: Final success state of an order
  - DLQ: Dead Letter Queue for failed messages
  - OPEN: Circuit breaker state when failing fast
  - FAILED: Final error state of an order
  - PROCESSED: Idempotency state indicating success

  @domain:Order @pattern:Saga @architecture:CQRS
  Scenario: Creación y procesamiento de un pedido exitoso (Saga Happy Path)
    Given a valid order payload from "External E-Commerce"
    When the system receives the order creation request
    Then a "OrderCreated" event is published with a unique CorrelationId
    And the saga orchestrator starts the order processing saga
    And the customer is validated
    And inventory is reserved
    And the order state transitions to "COMPLETED"

  @domain:Payment @pattern:CircuitBreaker @negative
  Scenario: Error handling por indisponibilidad de la pasarela externa
    Given an order in pending state
    When the external gateway experiences high latency
    Then the system applies exponential backoff for retries
    And if max attempts are reached, the message is sent to the DLQ
    And the circuit breaker transitions to "OPEN" state
    And the order state transitions to "FAILED"

  @domain:Customer @architecture:EventDriven
  Scenario: Detección y rechazo de eventos duplicados
    Given an existing webhook event with MessageId "ABC123"
    When the system receives a duplicate webhook event with MessageId "ABC123"
    Then the system ignores the duplicate event
    And no domain state is mutated
    And the system returns the previous result

  @domain:Audit @pattern:Outbox
  Scenario: Audit logging and Outbox emission
    Given a valid customer update request
    When the aggregate state is updated
    Then an Outbox event is safely persisted
    And the "CustomerUpdated" event is published
    And an audit log is generated for traceability
