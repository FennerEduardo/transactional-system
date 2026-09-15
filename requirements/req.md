# Plataforma Transaccional Distribuida Event-Driven
## Documento de Requerimientos y Arquitectura

**Versión:** 1.0  
**Tipo:** System Requirements Specification / Product & Technical Requirements  
**Estado:** Propuesta  
**Backend:** .NET  
**Frontend:** React + Next.js  
**Cloud:** AWS  
**CI/CD:** GitHub Actions  
**Containerización:** Docker  
**Arquitectura:** Distributed Systems + Event-Driven Architecture + CQRS + Saga

---

# 1. Resumen ejecutivo

Se requiere construir una plataforma transaccional distribuida capaz de recibir, procesar, normalizar y ejecutar operaciones provenientes de múltiples plataformas de e-commerce.

El sistema deberá administrar principalmente:

- Clientes.
- Pedidos.
- Pagos.
- Pasarelas de pago.
- Inventario/reservas cuando aplique.
- Estados transaccionales.
- Integraciones externas.
- Eventos de dominio.
- Historial completo de operaciones.
- Auditoría.
- Trazabilidad distribuida.
- Reintentos.
- Recuperación ante fallos.
- Procesamiento idempotente.

La solución deberá utilizar una arquitectura **Event-Driven** y aplicar **CQRS** para separar las responsabilidades de escritura y lectura.

Las operaciones transaccionales distribuidas deberán utilizar el patrón **Saga**, preferiblemente mediante una estrategia de **Orchestration** para los procesos críticos.

El sistema deberá diseñarse inicialmente para operar de manera eficiente en una infraestructura pequeña, permitiendo **escalamiento vertical como estrategia inicial**, pero sin introducir decisiones arquitectónicas que impidan posteriormente el **escalamiento horizontal**.

---

# 2. Objetivo del producto

Construir una plataforma central que permita integrar diferentes canales de e-commerce y ejecutar de forma confiable sus operaciones transaccionales.

Conceptualmente:

```text
                    ┌──────────────────────┐
                    │   E-Commerce #1      │
                    └──────────┬───────────┘
                               │
                    ┌──────────▼───────────┐
                    │   E-Commerce #2      │
                    └──────────┬───────────┘
                               │
                    ┌──────────▼───────────┐
                    │   E-Commerce #N      │
                    └──────────┬───────────┘
                               │
                               ▼
                  ┌─────────────────────────┐
                  │ Integration / Ingestion │
                  └────────────┬────────────┘
                               │
                               ▼
                  ┌─────────────────────────┐
                  │ Event Bus / Message     │
                  │ Broker                  │
                  └────────────┬────────────┘
                               │
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
        Customer Service   Order Service   Payment Service
              │                │                │
              └────────────────┼────────────────┘
                               ▼
                         Saga Orchestrator
                               │
                    ┌──────────┼──────────┐
                    ▼          ▼          ▼
                 Payment    Order      Customer
                 Gateway    System      System
                    │          │          │
                    └──────────┼──────────┘
                               ▼
                         Event Store /
                         Audit Trail
                               │
                               ▼
                     CQRS Read Models
                               │
                               ▼
                    React / Next.js UI
```

---

# 3. Principios arquitectónicos

La solución deberá cumplir los siguientes principios:

1. **Event-Driven Architecture**
2. **Domain-Driven Design**
3. **CQRS**
4. **Saga Pattern**
5. **Database-per-Service cuando exista separación real de bounded contexts**
6. **API-first**
7. **Idempotency by design**
8. **At-least-once message delivery**
9. **Eventual consistency donde sea apropiado**
10. **Strong consistency solamente donde sea necesaria**
11. **Immutable audit trail**
12. **Observability by default**
13. **Zero-trust entre componentes**
14. **Fail-fast + retry controlado**
15. **Dead Letter Queue**
16. **Outbox Pattern**
17. **Correlation ID / Causation ID**
18. **Infrastructure as Code**
19. **Container-first deployment**
20. **Horizontal scalability ready**

---

# 4. Alcance funcional

## 4.1 Gestión de clientes

El sistema deberá permitir:

- Crear clientes.
- Actualizar clientes.
- Buscar clientes.
- Consultar historial.
- Asociar clientes con pedidos.
- Asociar clientes con múltiples e-commerce.
- Mantener identificadores externos.
- Detectar duplicados.
- Resolver identidad entre diferentes plataformas.

Cada cliente deberá poseer:

```text
CustomerId
ExternalCustomerIds
SourceSystem
Email
Phone
Name
Addresses
Status
CreatedAt
UpdatedAt
Version
```

El sistema deberá diferenciar:

- Identidad interna.
- Identidad externa.
- Identidad del evento.
- Identidad de la transacción.

---

# 5. Gestión de pedidos

El sistema deberá soportar:

- Recepción de pedidos.
- Creación.
- Actualización.
- Cancelación.
- Consulta.
- Historial.
- Estados.
- Items.
- Totales.
- Impuestos.
- Descuentos.
- Shipping.
- Cliente asociado.
- Información del e-commerce origen.

Estados mínimos:

```text
RECEIVED
VALIDATING
VALIDATED
PAYMENT_PENDING
PAYMENT_PROCESSING
PAID
PROCESSING
COMPLETED
CANCELLED
FAILED
REFUND_PENDING
REFUNDED
```

Las transiciones deberán estar gobernadas por una máquina de estados y no por modificaciones arbitrarias desde cualquier componente.

---

# 6. Gestión de pagos

El sistema deberá abstraer las diferentes pasarelas mediante un contrato común.

Ejemplo:

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> AuthorizeAsync(
        PaymentRequest request,
        CancellationToken cancellationToken);

    Task<PaymentResult> CaptureAsync(
        PaymentRequest request,
        CancellationToken cancellationToken);

    Task<PaymentResult> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken);

    Task<PaymentStatus> GetStatusAsync(
        string transactionId,
        CancellationToken cancellationToken);
}
```

La arquitectura deberá permitir agregar nuevas pasarelas sin modificar el core transaccional.

Ejemplo:

```text
Payment Gateway
      │
      ├── Provider A
      ├── Provider B
      ├── Provider C
      └── Provider N
```

Cada pago deberá mantener:

```text
PaymentId
OrderId
CustomerId
Gateway
GatewayTransactionId
Amount
Currency
Status
AttemptNumber
IdempotencyKey
CreatedAt
ProcessedAt
FailureReason
```

---

# 7. Integraciones con e-commerce

Las integraciones deberán desacoplarse del dominio central.

Cada adapter deberá transformar el modelo externo hacia el modelo canónico interno.

```text
External E-Commerce
        │
        ▼
Integration Adapter
        │
        ▼
Canonical Event
        │
        ▼
Internal Domain
```

Ejemplo:

```csharp
public interface IECommerceAdapter
{
    Task<CanonicalOrder> MapOrderAsync(
        ExternalOrder order,
        CancellationToken cancellationToken);
}
```

Cada integración deberá manejar:

- Autenticación.
- Webhooks.
- Polling cuando sea necesario.
- Rate limits.
- Retries.
- Timeouts.
- Circuit breakers.
- Idempotencia.
- Mapping.
- Versionamiento de API.
- Errores específicos del proveedor.

---

# 8. Modelo de eventos

Los eventos serán ciudadanos de primera clase dentro del sistema.

Ejemplo:

```json
{
  "eventId": "01J...",
  "eventType": "OrderCreated",
  "eventVersion": 1,
  "occurredAt": "2026-09-15T14:00:00Z",
  "aggregateId": "order-123",
  "aggregateType": "Order",
  "correlationId": "correlation-456",
  "causationId": "event-previous",
  "source": "shopify-adapter",
  "tenantId": "tenant-001",
  "payload": {}
}
```

Todo evento deberá contener como mínimo:

- Event ID.
- Event type.
- Event version.
- Timestamp.
- Aggregate ID.
- Aggregate type.
- Correlation ID.
- Causation ID.
- Source.
- Payload.

---

# 9. Idempotencia

La idempotencia es un requisito crítico.

El sistema deberá garantizar que procesar dos veces el mismo mensaje no produzca dos operaciones de negocio.

Ejemplo:

```text
Webhook
   │
   ▼
MessageId = ABC123
   │
   ▼
Idempotency Store
   │
   ├── NOT PROCESSED → Process
   │
   └── PROCESSED → Return previous result
```

Se deberá implementar un mecanismo de deduplicación basado en:

```text
MessageId
+
ConsumerId
+
Operation
```

La restricción deberá estar respaldada por una operación atómica en persistencia.

---

# 10. Outbox Pattern

Los cambios de dominio y la publicación del evento no deberán depender de dos transacciones independientes.

Problema:

```text
Database Commit
      │
      ▼
Publish Event
      │
      X
    Failure
```

La solución deberá utilizar Outbox:

```text
BEGIN TRANSACTION

Update Aggregate

Insert Outbox Event

COMMIT

             ↓

Outbox Publisher

             ↓

Message Broker
```

Esto evitará inconsistencias entre la base de datos y el broker.

---

# 11. Saga Pattern

Las operaciones distribuidas deberán implementarse como Sagas.

Ejemplo de creación de pedido:

```text
Order Created
      │
      ▼
Validate Customer
      │
      ▼
Reserve Inventory
      │
      ▼
Authorize Payment
      │
      ▼
Confirm Order
      │
      ▼
Publish OrderCompleted
```

Si falla Payment:

```text
Reserve Inventory
       │
       X
Payment Failed
       │
       ▼
Release Inventory
       │
       ▼
Order Failed
```

Las compensaciones deberán ser explícitas.

Ejemplo:

```text
Action:
ReserveInventory

Compensation:
ReleaseInventory
```

---

# 12. Saga Orchestrator

Se recomienda una Saga basada en **orquestación** para las transacciones críticas.

Ejemplo:

```text
                    ┌─────────────────┐
                    │ Saga Orchestrator│
                    └────────┬────────┘
                             │
             ┌───────────────┼───────────────┐
             ▼               ▼               ▼
       Customer Service  Order Service  Payment Service
             │               │               │
             └───────────────┼───────────────┘
                             ▼
                         Final State
```

La Saga deberá persistir su estado.

```text
SagaId
TransactionId
CurrentStep
Status
StartedAt
CompletedAt
RetryCount
LastError
```

Estados:

```text
STARTED
RUNNING
WAITING
COMPLETED
COMPENSATING
COMPENSATED
FAILED
MANUAL_INTERVENTION
```

---

# 13. CQRS

El sistema deberá separar:

```text
Command Side
      │
      ▼
Domain Model
      │
      ▼
Events
      │
      ▼
Read Model
      │
      ▼
Query Side
```

### Command

Responsable de:

- Validar reglas.
- Ejecutar operaciones.
- Modificar estado.
- Generar eventos.

### Query

Responsable de:

- Consultas.
- Dashboards.
- Reportes.
- Historial.
- Búsqueda.

Las consultas no deberán ejecutar lógica de negocio transaccional.

---

# 14. Read Models

Se podrán construir modelos optimizados para consultas.

Ejemplo:

```text
Order
 ├── Customer
 ├── Payment
 ├── Items
 ├── Status
 ├── Gateway
 └── Timeline
```

En lugar de ejecutar múltiples joins complejos, el frontend podrá consultar:

```http
GET /api/orders/{id}/summary
```

Respuesta:

```json
{
  "orderId": "123",
  "customer": {},
  "payment": {},
  "items": [],
  "status": "PAID",
  "timeline": []
}
```

---

# 15. Historial y trazabilidad

El sistema deberá conservar el historial completo de las operaciones relevantes.

Ejemplo:

```text
OrderCreated
      ↓
CustomerValidated
      ↓
PaymentRequested
      ↓
PaymentAuthorized
      ↓
OrderConfirmed
```

Cada transición deberá poder reconstruirse posteriormente.

El historial deberá ser:

- Auditable.
- Inmutable.
- Consultable.
- Correlacionable.

---

# 16. Correlation ID y Causation ID

Cada flujo deberá tener un `CorrelationId`.

Ejemplo:

```text
CorrelationId = 123
```

Todos los eventos relacionados deberán conservarlo:

```text
OrderCreated
PaymentRequested
PaymentAuthorized
OrderCompleted
```

Además:

```text
CausationId
```

identificará el evento que provocó el actual.

Esto permitirá reconstruir:

```text
Root Event
   ↓
Event A
   ↓
Event B
   ↓
Event C
```

---

# 17. Distributed Tracing

Se deberá implementar **OpenTelemetry**.

Cada request deberá generar un trace.

Ejemplo:

```text
HTTP Request
    │
    ├── API
    │
    ├── Order Service
    │
    ├── Payment Service
    │
    └── External Gateway
```

Métricas mínimas:

- Request latency.
- Error rate.
- Throughput.
- Queue depth.
- Consumer lag.
- Retry count.
- Failed messages.
- Saga duration.
- Payment latency.
- External API latency.

---

# 18. Retries

No todas las excepciones deberán reintentarse.

### Retryable

```text
Timeout
HTTP 408
HTTP 429
HTTP 500
HTTP 502
HTTP 503
HTTP 504
Network failure
```

### Non-retryable

```text
Invalid request
Invalid credentials
Insufficient funds
Invalid card
Business rule violation
Malformed payload
```

Se deberá utilizar **exponential backoff + jitter**.

Ejemplo:

```text
Attempt 1 → 1 sec
Attempt 2 → 2 sec
Attempt 3 → 4 sec
Attempt 4 → 8 sec
Attempt 5 → DLQ
```

Los valores deberán ser configurables.

---

# 19. Dead Letter Queue

Todo mensaje que exceda el número máximo de reintentos deberá enviarse a una DLQ.

```text
Queue
  │
  ▼
Consumer
  │
  X
Failure
  │
  ▼
Retry
  │
  X
Max Attempts
  │
  ▼
DLQ
```

La plataforma deberá proporcionar herramientas para:

- Visualizar mensajes.
- Consultar error.
- Reprocesar.
- Ignorar.
- Mover nuevamente a la cola.
- Registrar intervención manual.

---

# 20. Circuit Breaker

Las integraciones externas deberán utilizar circuit breakers.

Estados:

```text
CLOSED
   │
   ▼
Failures
   │
   ▼
OPEN
   │
   ▼
Timeout
   │
   ▼
HALF-OPEN
   │
   ├── Success → CLOSED
   └── Failure → OPEN
```

Esto evitará que la indisponibilidad de un proveedor degrade todo el sistema.

---

# 21. Timeouts

Todos los llamados externos deberán tener timeout explícito.

Nunca deberá existir:

```csharp
HttpClient.GetAsync(...)
```

sin una política de timeout apropiada.

Las políticas deberán estar centralizadas.

---

# 22. Consistencia

La arquitectura deberá distinguir:

### Strong consistency

Para:

- Idempotency keys.
- Estado crítico de una transacción.
- Operaciones financieras.
- Actualización de un aggregate.

### Eventual consistency

Para:

- Dashboards.
- Reportes.
- Search.
- Timeline.
- Estadísticas.
- Read models.

No deberá utilizarse consistencia fuerte como mecanismo general para resolver problemas de integración distribuida.

---

# 23. Bounded Contexts

Se recomienda dividir inicialmente el dominio en:

```text
Identity / Customer
Order
Payment
Integration
Transaction
Audit
Notification
Reporting
```

Una posible estructura:

```text
src/

  Services/
    Customer/
    Order/
    Payment/
    Integration/
    Transaction/

  BuildingBlocks/
    Domain/
    Application/
    Infrastructure/
    Messaging/
    Observability/
```

La separación física podrá comenzar siendo moderada.

No se deberá caer inicialmente en un **microservices zoo**.

La arquitectura deberá permitir evolucionar hacia microservicios cuando la carga, ownership o requisitos de escalamiento lo justifiquen.

---

# 24. Estrategia recomendada: Modular Monolith → Distributed Services

Para la primera versión se recomienda evaluar una arquitectura:

**Modular Monolith + Event Bus + Workers**

en lugar de desplegar inmediatamente 10+ microservicios.

Esto permite:

- Menor complejidad operacional.
- Menor costo.
- Desarrollo más rápido.
- Transacciones locales más sencillas.
- Mantener bounded contexts.
- Mantener contratos de eventos.
- Migrar posteriormente servicios con mayor carga.

La arquitectura deberá mantener límites claros para que un módulo pueda convertirse posteriormente en servicio independiente.

---

# 25. Backend

Stack obligatorio:

```text
.NET 10+
ASP.NET Core
C#
Entity Framework Core
PostgreSQL
Redis
OpenTelemetry
Polly
FluentValidation
Serilog
```

La versión exacta de .NET deberá fijarse durante el inicio del proyecto considerando el LTS vigente.

---

# 26. Messaging

Se requiere un broker de mensajes.

En AWS se deberá evaluar:

### Opción A

Amazon SQS + SNS

Ventajas:

- Serverless.
- Alta disponibilidad.
- Bajo mantenimiento.
- Integración nativa AWS.

### Opción B

Amazon MSK / Kafka

Ventajas:

- Event streaming.
- Replay.
- Alto throughput.
- Retención de eventos.
- Consumer groups.
- Ecosistema Kafka.

Para una primera implementación con volumen moderado, **SQS/SNS puede reducir significativamente la complejidad operacional**.

Kafka deberá considerarse cuando exista una necesidad real de:

- Event replay masivo.
- Streaming.
- Alto throughput.
- Múltiples consumidores independientes.
- Retención prolongada de eventos.

---

# 27. Persistencia

Base transaccional recomendada:

```text
PostgreSQL
```

Debe soportar:

- ACID.
- Constraints.
- Transactions.
- JSONB cuando sea apropiado.
- Indexación.
- Partitioning futuro.

No se deberá utilizar JSON como sustituto del modelado relacional.

---

# 28. Redis

Redis podrá utilizarse para:

- Distributed cache.
- Rate limiting.
- Locks cuando estén correctamente diseñados.
- Session state si fuese necesario.
- Cache de consultas.

Redis no deberá utilizarse como fuente primaria de verdad para operaciones financieras.

---

# 29. Frontend

Stack:

```text
React
Next.js
TypeScript
TanStack Query
Zod
React Hook Form
```

El frontend deberá consumir APIs mediante contratos versionados.

La arquitectura deberá separar:

```text
UI State
Server State
Domain State
Authentication State
```

---

# 30. Panel administrativo

El sistema deberá incluir:

### Dashboard

- Pedidos procesados.
- Pedidos fallidos.
- Pagos exitosos.
- Pagos fallidos.
- Transactions pendientes.
- Sagas activas.
- Sagas fallidas.
- Mensajes DLQ.
- Integraciones con problemas.

### Pedidos

- Búsqueda.
- Filtros.
- Detalle.
- Timeline.
- Estado.
- Cliente.
- Pago.

### Clientes

- Perfil.
- Identificadores externos.
- Pedidos.
- Historial.

### Pagos

- Estado.
- Gateway.
- Intentos.
- Errores.
- Transaction ID.

### Sagas

- Saga ID.
- Estado.
- Paso actual.
- Duración.
- Error.
- Reintentos.

### Eventos

- Event ID.
- Event Type.
- Correlation ID.
- Causation ID.
- Timestamp.
- Payload.
- Consumer.
- Resultado.

### DLQ

- Mensajes fallidos.
- Causa.
- Número de intentos.
- Reprocesamiento.

---

# 31. Seguridad

Se deberá implementar:

- OAuth2/OIDC.
- JWT.
- RBAC.
- MFA para administración cuando corresponda.
- Secrets fuera del código.
- AWS Secrets Manager.
- Encryption at rest.
- TLS.
- Encryption in transit.
- Audit logging.
- Rate limiting.
- Input validation.
- OWASP API Security.

Los secretos nunca deberán estar:

```text
Git repository
Docker image
Frontend
logs
configuration committed
```

---

# 32. Autorización

Roles mínimos:

```text
SUPER_ADMIN
ADMIN
OPERATIONS
SUPPORT
AUDITOR
READ_ONLY
```

Los permisos deberán aplicarse tanto en frontend como backend.

La seguridad real deberá estar siempre en backend.

---

# 33. Multi-tenancy

La arquitectura deberá quedar preparada para múltiples merchants/tenants.

Cada transacción deberá identificar:

```text
TenantId
```

Los datos deberán aislarse lógicamente.

La estrategia inicial podrá ser:

```text
Shared Database
Shared Schema
TenantId
```

con evolución posterior a:

```text
Database per Tenant
```

si los requerimientos de seguridad, volumen o aislamiento lo justifican.

---

# 34. API

Se deberán proporcionar APIs versionadas:

```text
/api/v1/customers
/api/v1/orders
/api/v1/payments
/api/v1/transactions
/api/v1/sagas
/api/v1/events
```

Se deberá utilizar:

- OpenAPI.
- Swagger.
- DTOs.
- Validation.
- Pagination.
- Filtering.
- Sorting.
- Error contracts.

---

# 35. Error Contract

Todos los errores deberán utilizar una estructura consistente.

Ejemplo:

```json
{
  "type": "https://api.example.com/errors/payment-failed",
  "title": "Payment failed",
  "status": 402,
  "code": "PAYMENT_DECLINED",
  "message": "The payment was declined",
  "traceId": "abc123",
  "correlationId": "xyz789"
}
```

Se recomienda seguir RFC 9457 Problem Details.

---

# 36. Concurrency

El sistema deberá prevenir:

- Double payment.
- Double order creation.
- Double refund.
- Concurrent state corruption.

Se recomienda utilizar:

```text
Optimistic Concurrency
+
Version column
+
Idempotency
```

Ejemplo:

```text
Order.Version = 10

UPDATE orders
SET status = 'PAID',
    version = 11
WHERE id = @id
AND version = 10
```

---

# 37. Transaction ID

Cada flujo financiero deberá tener un identificador de transacción independiente.

Ejemplo:

```text
TransactionId
SagaId
CorrelationId
OrderId
PaymentId
```

No deberán confundirse estos identificadores.

Relación:

```text
Transaction
 ├── Saga
 ├── Order
 ├── Payment
 ├── Events
 └── External Calls
```

---

# 38. Audit Trail

Toda acción relevante deberá registrarse.

Ejemplo:

```text
WHO
WHAT
WHEN
WHERE
WHY
RESULT
```

Datos:

```text
AuditId
UserId
Service
Action
EntityType
EntityId
Before
After
Timestamp
CorrelationId
IpAddress
```

La auditoría deberá ser append-only.

---

# 39. Observabilidad

Se deberá implementar:

### Logs

Structured logging:

```json
{
  "timestamp": "...",
  "level": "Error",
  "service": "PaymentService",
  "traceId": "...",
  "correlationId": "...",
  "message": "Payment failed"
}
```

### Metrics

- CPU.
- Memory.
- HTTP latency.
- Errors.
- Queue depth.
- Processing rate.
- Retry count.
- Saga failures.
- Payment failures.

### Tracing

OpenTelemetry.

---

# 40. AWS

Arquitectura inicial recomendada:

```text
                    Route 53
                       │
                       ▼
                  CloudFront
                       │
                       ▼
                 Application Load
                    Balancer
                       │
                       ▼
                ECS Fargate
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
       API        Background      Workers
                    Workers
          │            │
          └────────────┼────────────┘
                       │
                ┌──────┼──────┐
                ▼      ▼      ▼
              RDS     Redis   SQS/SNS
                │
                ▼
              S3
```

---

# 41. AWS Services

Componentes sugeridos:

| Necesidad | AWS |
|---|---|
| Compute | ECS Fargate |
| Load Balancing | ALB |
| Database | RDS PostgreSQL |
| Cache | ElastiCache Redis |
| Messaging | SQS/SNS |
| Files | S3 |
| Secrets | Secrets Manager |
| Monitoring | CloudWatch |
| Tracing | X-Ray / OpenTelemetry |
| CDN | CloudFront |
| DNS | Route 53 |
| Container Registry | ECR |
| Security | IAM / WAF |
| CI/CD | GitHub Actions |
| Infrastructure | Terraform |

---

# 42. Containerización

Todos los servicios deberán ejecutarse mediante Docker.

Ejemplo:

```text
Dockerfile
docker-compose.yml
docker-compose.dev.yml
```

Los containers deberán:

- Ser stateless.
- No almacenar datos persistentes localmente.
- Exponer health checks.
- Ejecutar como usuario no-root.
- Tener imágenes mínimas.
- Utilizar multi-stage builds.

---

# 43. Health Checks

Cada servicio deberá exponer:

```http
GET /health
GET /health/live
GET /health/ready
```

Diferenciando:

```text
Liveness
Readiness
Dependency health
```

---

# 44. Escalamiento

## Fase inicial

Escalamiento vertical:

```text
ECS Task
   │
   ├── More CPU
   └── More RAM
```

## Evolución

Escalamiento horizontal:

```text
              Load Balancer
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
      Task 1      Task 2      Task N
```

Los servicios deberán ser stateless para permitir esto.

---

# 45. Worker Scaling

Los workers deberán escalar según:

```text
Queue Depth
Message Age
Processing Time
CPU
```

Ejemplo:

```text
Queue depth > threshold
        ↓
Increase consumers
```

---

# 46. CI/CD

Todo cambio deberá pasar por:

```text
Pull Request
     ↓
Lint
     ↓
Build
     ↓
Unit Tests
     ↓
Integration Tests
     ↓
Contract Tests
     ↓
Security Scan
     ↓
Docker Build
     ↓
Docker Scan
     ↓
Deploy Staging
     ↓
E2E Tests
     ↓
Approval
     ↓
Production
```

---

# 47. GitHub Actions

Pipeline mínimo:

```text
.github/
  workflows/
    ci.yml
    cd-staging.yml
    cd-production.yml
    security.yml
```

Branches:

```text
main
develop
feature/*
hotfix/*
```

Se recomienda proteger `main`.

---

# 48. Testing

La estrategia deberá tener varios niveles.

## Unit Tests

Para:

- Domain.
- Business rules.
- Value objects.
- State transitions.
- Saga logic.

## Integration Tests

Para:

- PostgreSQL.
- Redis.
- Message broker.
- External integrations.

## Contract Tests

Para validar:

```text
Producer ↔ Consumer
```

## E2E

Para validar:

```text
E-Commerce
    ↓
API
    ↓
Order
    ↓
Payment
    ↓
Saga
    ↓
Final state
```

---

# 49. Chaos / Resilience Testing

El sistema deberá probar escenarios de fallo.

Casos mínimos:

1. Payment provider unavailable.
2. Database temporarily unavailable.
3. Message broker unavailable.
4. Duplicate webhook.
5. Duplicate payment request.
6. Consumer crashes.
7. Network timeout.
8. Message processed but acknowledgment lost.
9. Saga worker crashes.
10. Application restart during transaction.
11. External provider returns 500.
12. External provider returns timeout.

El sistema deberá demostrar que puede recuperar el flujo sin duplicar operaciones.

---

# 50. Reprocesamiento

Los eventos deberán poder ser reprocesados de manera controlada.

Debe existir:

```text
Replay
Retry
Re-drive
Compensation
Manual Intervention
```

El replay nunca deberá generar efectos financieros duplicados.

Por esto:

```text
Replay
 +
Idempotency
 =
Safe Recovery
```

---

# 51. Disaster Recovery

Se deberán definir:

### RPO

Objetivo inicial:

```text
≤ 15 minutos
```

### RTO

Objetivo inicial:

```text
≤ 1 hora
```

Estos valores deberán ser confirmados con negocio.

Se deberán implementar:

- Automated backups.
- Database snapshots.
- Backup retention.
- Recovery procedures.
- Disaster recovery documentation.

---

# 52. Data retention

El sistema deberá definir políticas para:

- Eventos.
- Logs.
- Auditoría.
- Pagos.
- Datos personales.
- Información de debugging.

Los datos financieros y de auditoría deberán tener políticas de retención acordes con las obligaciones legales y contractuales aplicables.

---

# 53. Protección de datos

El sistema deberá contemplar requisitos aplicables a:

- Datos personales.
- Información financiera.
- PCI DSS cuando corresponda.

Los datos sensibles de tarjetas nunca deberán almacenarse directamente si el proveedor permite tokenización.

Idealmente:

```text
Frontend
   ↓
Payment Provider
   ↓
Token
   ↓
Backend
```

en lugar de:

```text
Frontend
   ↓
Backend
   ↓
Raw Card Data
```

---

# 54. Requisitos no funcionales

## Disponibilidad

Objetivo inicial:

```text
99.9%
```

## Latencia

API interna:

```text
p95 < 300 ms
```

Las integraciones externas deberán medirse separadamente.

## Throughput

El sistema deberá diseñarse inicialmente para soportar al menos:

```text
100 requests/sec
```

con capacidad de incremento mediante scaling.

El valor definitivo deberá establecerse después de conocer volumen real.

---

# 55. Seguridad operacional

Debe existir:

- IAM least privilege.
- Rotación de secrets.
- Security groups.
- Private subnets para RDS.
- WAF.
- TLS.
- Container vulnerability scanning.
- Dependency scanning.
- SAST.
- DAST para endpoints críticos.

---

# 56. Arquitectura lógica

```text
┌───────────────────────────────────────────────────────┐
│                    Presentation                       │
│                                                       │
│             Next.js / React / TypeScript              │
└──────────────────────────┬────────────────────────────┘
                           │
                           ▼
┌───────────────────────────────────────────────────────┐
│                       API                              │
│                   ASP.NET Core                         │
└──────────────────────────┬────────────────────────────┘
                           │
                ┌──────────┴──────────┐
                ▼                     ▼
          Command Side           Query Side
                │                     │
                ▼                     ▼
          Domain Model          Read Models
                │                     │
                ▼                     │
             Outbox                   │
                │                     │
                ▼                     │
          Event Broker ───────────────┘
                │
        ┌───────┼────────┐
        ▼       ▼        ▼
      Order  Payment  Customer
     Worker   Worker   Worker
        │       │        │
        └───────┼────────┘
                ▼
           Saga Engine
                │
                ▼
        External Providers
```

---

# 57. Modelo de dominio inicial

Entidades principales:

```text
Customer
Order
OrderItem
Payment
PaymentAttempt
Transaction
Saga
SagaStep
Integration
ExternalAccount
Event
OutboxMessage
InboxMessage
AuditEntry
```

Value Objects:

```text
Money
Currency
Address
Email
Phone
OrderNumber
ExternalId
IdempotencyKey
```

---

# 58. Estados transaccionales

Una transacción deberá poder pasar por:

```text
RECEIVED
VALIDATING
PROCESSING
WAITING_EXTERNAL
COMPLETED
RETRYING
FAILED
COMPENSATING
COMPENSATED
MANUAL_INTERVENTION
```

No se deberá permitir:

```text
COMPLETED → PROCESSING
```

sin una operación explícita de negocio que lo justifique.

---

# 59. Requisitos de trazabilidad

Dado un `OrderId`, el operador deberá poder consultar:

```text
Order
  │
  ├── Customer
  ├── Payment
  ├── Saga
  ├── Events
  ├── Retries
  ├── External Calls
  ├── Errors
  └── Audit Entries
```

También deberá poder iniciar la búsqueda desde:

```text
CorrelationId
TransactionId
SagaId
PaymentId
ExternalTransactionId
EventId
```

y llegar al resto del flujo.

---

# 60. Requisitos de operación manual

El sistema deberá contemplar que una Saga pueda quedar en:

```text
MANUAL_INTERVENTION
```

Ejemplo:

```text
Payment Provider
       │
       ▼
Unknown Result
       │
       ▼
Payment status = UNKNOWN
       │
       ▼
DO NOT CHARGE AGAIN
       │
       ▼
Query Provider
       │
       ├── PAID
       │
       └── NOT PAID
```

Este punto es especialmente importante para evitar dobles cobros.

---

# 61. Exactly-once vs At-least-once

La arquitectura no deberá asumir que el broker proporciona exactamente una vez.

El modelo deberá ser:

```text
At-least-once delivery
+
Idempotent consumers
+
Transactional Outbox
+
Inbox/Deduplication
```

Esto proporciona semántica operacional equivalente a exactamente-una-vez para las operaciones donde sea posible, sin depender de una garantía imposible o excesivamente costosa a nivel distribuido.

---

# 62. Versionamiento de eventos

Los eventos deberán versionarse:

```text
OrderCreated.v1
OrderCreated.v2
```

Nunca se deberá modificar silenciosamente el contrato de un evento existente.

Se deberán soportar estrategias de:

- Backward compatibility.
- Event versioning.
- Consumer migration.
- Deprecation.

---

# 63. Backward compatibility

Las APIs y eventos deberán evolucionar de forma compatible.

Cambios breaking deberán requerir:

```text
v2
```

en lugar de modificar:

```text
v1
```

---

# 64. Requisitos de documentación

Se deberá producir:

1. Architecture Decision Records.
2. OpenAPI specification.
3. Event catalog.
4. Database diagrams.
5. Sequence diagrams.
6. Saga diagrams.
7. Deployment architecture.
8. Runbooks.
9. Disaster recovery procedures.
10. Integration guides.

---

# 65. Architecture Decision Records

Toda decisión arquitectónica importante deberá documentarse.

Ejemplo:

```text
ADR-001
Decision: PostgreSQL

ADR-002
Decision: SQS/SNS vs Kafka

ADR-003
Decision: Saga Orchestration

ADR-004
Decision: Modular Monolith

ADR-005
Decision: Transactional Outbox
```

---

# 66. Definition of Done

Una funcionalidad no deberá considerarse terminada hasta cumplir:

- Código implementado.
- Unit tests.
- Integration tests cuando corresponda.
- Logging.
- Metrics.
- Tracing.
- Error handling.
- Idempotency.
- Documentation.
- Security validation.
- Code review.
- CI passing.
- Docker image generated.
- Deployment validated.

---

# 67. MVP

El MVP deberá concentrarse en:

### Integraciones

- 1 e-commerce.
- 1 payment gateway.

### Dominio

- Customer.
- Order.
- Payment.
- Transaction.
- Saga.

### Infraestructura

- PostgreSQL.
- Redis.
- SQS/SNS.
- Docker.
- ECS.
- RDS.
- CloudWatch.
- GitHub Actions.

### Frontend

- Login.
- Dashboard.
- Customers.
- Orders.
- Payments.
- Transaction timeline.
- Saga monitoring.

---

# 68. Segunda fase

Agregar:

- Segundo e-commerce.
- Segundo payment gateway.
- DLQ management.
- Replay.
- Advanced reporting.
- Advanced observability.
- Autoscaling.
- Multi-tenant.
- Advanced RBAC.

---

# 69. Tercera fase

Evolución hacia:

```text
Independent Services
       +
Independent Scaling
       +
Advanced Event Streaming
       +
Multi-region
       +
Advanced DR
```

Los componentes que demuestren necesidad real de escalamiento podrán extraerse primero:

```text
Payment Service
Integration Service
Notification Service
Reporting Service
```

---

# 70. Criterios de aceptación arquitectónicos

La plataforma deberá demostrar como mínimo:

### AC-01 — Duplicate Order

Si el mismo evento de pedido llega 10 veces:

```text
Expected:
1 Order
10 processing attempts
0 duplicate orders
```

### AC-02 — Duplicate Payment

Si un mensaje de pago se entrega múltiples veces:

```text
Expected:
1 financial operation
N duplicate messages ignored
```

### AC-03 — Payment Failure

Si el pago falla:

```text
Payment Failed
     ↓
Saga Compensation
     ↓
Order Failed
```

### AC-04 — Worker Crash

Si un worker muere después de procesar pero antes del ACK:

```text
Message redelivered
     ↓
Idempotency check
     ↓
No duplicate side effect
```

### AC-05 — External Timeout

Si una pasarela tarda demasiado:

```text
Timeout
 ↓
Retry
 ↓
Circuit breaker
 ↓
DLQ / Manual intervention
```

### AC-06 — Full Traceability

Dado un `CorrelationId`, el operador deberá poder reconstruir todo el flujo.

### AC-07 — Recovery

Una Saga fallida deberá poder continuar o compensarse sin perder su estado.

### AC-08 — Horizontal Scaling

Agregar una segunda instancia de un worker no deberá producir duplicación de operaciones.

---

# 71. Principales riesgos arquitectónicos

## Riesgo 1 — Complejidad prematura

Implementar demasiados microservicios desde el día uno puede incrementar considerablemente:

- Costos.
- DevOps.
- Latencia.
- Debugging.
- Consistencia distribuida.

Mitigación:

**Modular Monolith + eventos + workers inicialmente.**

---

## Riesgo 2 — Double Payment

Es el riesgo más crítico.

Mitigación:

```text
Idempotency Key
+
Payment State Machine
+
Provider Transaction ID
+
Inbox
+
Saga
```

---

## Riesgo 3 — Eventos duplicados

Mitigación:

```text
Inbox Pattern
+
Unique Constraint
+
Idempotent Consumer
```

---

## Riesgo 4 — Poison Messages

Mitigación:

```text
Retry Policy
+
DLQ
+
Monitoring
+
Manual Replay
```

---

## Riesgo 5 — Eventual Consistency

El usuario puede crear un pedido y consultar inmediatamente una vista que todavía no refleja el estado final.

Mitigación:

- Estado transaccional en command side.
- Read model con indicadores de sincronización.
- UX que diferencie `PROCESSING` de `COMPLETED`.

---

# 72. Principio fundamental de diseño

El sistema deberá tratar una transacción distribuida como un **workflow durable**, no como una única transacción SQL.

En otras palabras:

```text
NO:

BEGIN TRANSACTION
    Order
    Customer
    Payment
    External API
COMMIT
```

Sino:

```text
Command
   ↓
Aggregate
   ↓
Event
   ↓
Saga
   ↓
Step 1
   ↓
Step 2
   ↓
Step 3
   ↓
Compensation if required
   ↓
Final Event
```

---

# 73. Resultado esperado

La arquitectura final deberá proporcionar:

```text
                    RELIABILITY
                         ▲
                         │
              ┌──────────┴──────────┐
              │                     │
        Idempotency              Saga
              │                     │
              └──────────┬──────────┘
                         │
EVENTS ─────────────── PLATFORM ─────────────── CQRS
                         │
              ┌──────────┴──────────┐
              │                     │
        Observability          Scalability
              │                     │
              └──────────┬──────────┘
                         │
                       AWS
```

El objetivo no es simplemente construir una API de pedidos.

El objetivo es construir una **plataforma transaccional resiliente**, capaz de mantener la trazabilidad completa de una operación distribuida aun cuando existan errores de red, duplicación de mensajes, reinicios de servicios, indisponibilidad de proveedores externos o fallos parciales.

La arquitectura deberá asumir desde el diseño que:

> **Los mensajes pueden duplicarse, los servicios pueden fallar, las redes pueden fallar y las respuestas externas pueden ser ambiguas.**

Por ello, **idempotencia, Outbox, Inbox, Saga, retries, DLQ, observabilidad y trazabilidad no son features opcionales; son requisitos fundamentales del sistema.**