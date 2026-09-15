# 🚀 AI AGENT MASTER IMPLEMENTATION PROMPT
## Feature: Plataforma Transaccional Distribuida Event-Driven (Spec Hash: c4de3a94)
## Architecture: CQRS | Stack: CSHARP (dotnet10)
## Prompt Version / Audit Hash: prt_48b59b2b
## Author / Developer: Fenner Eduardo González C. <fennereduardo@gmail.com> (source: git)
## Executed At: 2026-09-15T14:51:34.771Z
## Audit Record ID: rec_1789483894771_2f635af2

### 📌 Context Files to Read & Follow:
- @.ghkgovernance.yaml
- @features/transactional-system.feature
- @generated-specs/plataformatransaccionaldistribuidaeventdriven.contract.php
- @generated-specs/ADR-001-architecture-decisions.md
- @generated-specs/openapi.json
- @generated-specs/docker-compose.yml

### 🛠️ Technical Guardrails & Stack Specifications:
- **Language**: csharp (dotnet10)
- **Persistence**: ef-core + postgresql
- **Validation**: fluent-validation
- **Testing Framework**: xunit

### 🐳 Docker Execution Sandbox & Host Isolation Guardrails:
> **IMPORTANT**: If your host operating system lacks the native runtime SDK (CSHARP), DO NOT install heavy packages directly on the host machine.
> Execute all compilation, migrations, and test runs inside the isolated Docker container:
> 
> ```bash
> # Start database and infrastructure services
> docker compose up -d
> 
> # Execute test suite inside Docker sandbox container:
> docker compose run --rm app dotnet test
> ```

### 🎯 Mandatory Step-by-Step Implementation Flow:

#### Phase 1: Pure Domain Layer
1. Read the feature specification in `features/transactional-system.feature` and contract in `generated-specs/plataformatransaccionaldistribuidaeventdriven.contract.php`.
2. Implement pure domain Entities, Value Objects, and Domain Events.
3. Ensure zero dependencies on external frameworks or database drivers in the domain core.

#### Phase 2: Application Use Cases & Infrastructure
1. Implement the Repository Port interface using EF-CORE (postgresql).
2. Implement Controllers/Handlers to process HTTP requests and return appropriate status codes (e.g. 201 Created, 400 Bad Request).
3. Apply validation using fluent-validation.

#### Phase 3: Automated Unit & Feature Testing
1. Implement automated test cases in XUNIT matching all scenarios in `features/transactional-system.feature`.
2. Assert HTTP response status codes, payload structures, and event emissions.
3. If host environment lacks SDK, run verification inside Docker sandbox (`docker compose run --rm app dotnet test`).
4. Ensure 100% scenario pass rate.
