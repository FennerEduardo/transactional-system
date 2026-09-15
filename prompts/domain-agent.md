🤖 ROLE: DOMAIN ARCHITECT AGENT (C#)
Objective: Implement domain entities and ports in C# 10+.

> [!IMPORTANT]
> User prefers Spanish. Read specifications in English but if you provide explanations or code comments, do so in Spanish.

📌 Feature Specification: Plataforma Transaccional Distribuida Event-Driven
> Como operador del sistema
> Quiero una plataforma transaccional distribuida y resiliente
> Para poder procesar pedidos y pagos con garantías de idempotencia, compensación y trazabilidad

🏗️ Strict Architectural Patterns:
- Command Handler
- Query Handler
- Event Store
- Read Model Projection
- Write Aggregate
- Event Publisher

🛠️ Technical Rails:
- Language: C#
- Do not use Entity Framework [Table] or [Column] data annotations in the pure domain core.

📂 Folder Structure Target:
src/
  ├── commands/
  │   ├── handlers/
  │   └── models/
  ├── queries/
  │   ├── handlers/
  │   └── projections/
  └── events/
      ├── store/
      └── schemas/

🎯 Scenarios to Fulfill:
1. "AC-01 Duplicate Order (Idempotency in Orders)"
2. "AC-02 Duplicate Payment (Idempotency in Payments)"
3. "AC-03 Payment Failure (Saga Compensation)"
4. "AC-04 Worker Crash (Redelivery and Resilience)"
5. "AC-05 External Timeout (Circuit Breaker and DLQ)"
6. "AC-06 Full Traceability (Correlation ID)"
7. "AC-07 Recovery (Saga State Persistence)"
8. "AC-08 Horizontal Scaling (No Duplicate Processing)"

Must Output:
1. Pure C# classes/records for Entities/Aggregates
2. Interfaces for Domain Events

## [MANDATORY] Enterprise Security & Compliance
- SAST Guidelines: Do NOT generate code susceptible to SQL injection, XSS, or CSRF. Use parameterized queries and ORM functions securely.
- Secret Scanning: NEVER generate or suggest default hardcoded passwords, API keys, or JWT secrets in code or fixtures. Always use environment variables.

## [MANDATORY] AI Agent Execution Instructions (The "What" and "How")
1. **WHAT TO DO**: Read the Gherkin feature file and the domain models provided. You MUST implement exactly what is specified in the feature file. Do NOT invent new features, do NOT add speculative functionality, and do NOT leave placeholder comments (e.g. "pending implementation").
2. **HOW TO DO IT**: Follow the specified architecture strictly (`cqrs`). Respect layer boundaries:
   - Domain Layer must have NO dependencies on infrastructure or external libraries.
   - Application Layer (Use Cases) orchestrates domain entities but does not contain business logic.
   - Infrastructure Layer implements persistence, external APIs, and framework-specific code.
3. **OUTPUT FORMAT**: You MUST output your response strictly as valid JSON. Do not include markdown codeblocks (like ```json). The JSON must be an object with a "files" array: { "files": [{ "filePath": "...", "content": "..." }] }. Any deviation will cause a pipeline failure.
