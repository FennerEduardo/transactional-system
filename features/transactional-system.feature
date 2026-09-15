@transactional @event-driven
Feature: Plataforma Transaccional Distribuida Event-Driven
  Como un usuario del sistema transaccional
  Quiero que el sistema procese pagos y eventos de forma distribuida
  Para garantizar escalabilidad, resiliencia e idempotencia

  # Glossary
  # Order: A commercial transaction request by a customer.
  # Command: An intent to change the state of the system.
  # Webhook: An HTTP callback triggered by an external event.
  # Idempotency: The property of an operation that can be applied multiple times without changing the result beyond the initial application.

  Background:
    Given a valid customer

  @happy-path @observability
  Scenario: A valid Order is placed via the API
    When the customer creates an order
    Then the order should be created
    And a "OrderCreatedEvent" is published with a unique CorrelationId

  @idempotency
  Scenario: The system receives a duplicate Webhook event
    Given an existing webhook event with MessageId "abc123"
    When the system receives a duplicate webhook event with MessageId "abc123"
    Then the system ignores the duplicate event

  @outbox
  Scenario: The system publishes a Domain Event using the Outbox Pattern
    When the "CustomerUpdatedEvent" is published
    Then an Outbox event is safely persisted
    And the outbox relay eventually publishes the event to the message broker
