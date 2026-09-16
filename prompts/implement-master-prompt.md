# 🚀 AI AGENT MASTER IMPLEMENTATION PROMPT
## Feature: Transactional System (Spec Hash: a5b7ba91)
## Architecture: HEXAGONAL | Stack: CSHARP (dotnet-aspnetcore)
## Prompt Version / Audit Hash: prt_064fe64a
## Author / Developer: Fenner Eduardo González C. <fennereduardo@gmail.com> (source: git)

### 📌 Context Files to Read & Follow:
- @features/transactional-system.feature
- @transactionalsystem.contract.cs
- @ADR-001-architecture-decisions.md
- @openapi.json
- @docker-compose.yml

### 🛠️ Technical Guardrails & Stack Specifications:
- **Language**: csharp (dotnet-aspnetcore)
- **Persistence**: entity-framework-core + postgresql
- **Validation**: zod
- **Testing Framework**: reqnroll-xunit

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
1. Read the feature specification in `features/transactional-system.feature` and contract in `transactionalsystem.contract.cs`.
2. Implement pure domain Entities, Value Objects, and Domain Events.
3. Ensure zero dependencies on external frameworks or database drivers in the domain core.

#### Phase 2: Application Use Cases & Infrastructure
1. Implement the Repository Port interface using ENTITY-FRAMEWORK-CORE (postgresql).
2. Implement Controllers/Handlers to process HTTP requests and return appropriate status codes (e.g. 201 Created, 400 Bad Request).
3. Apply validation using zod.

#### Phase 3: Automated Unit & Feature Testing
1. Implement automated test cases in REQNROLL-XUNIT matching all scenarios in `features/transactional-system.feature`.
2. Assert HTTP response status codes, payload structures, and event emissions.
3. If host environment lacks SDK, run verification inside Docker sandbox (`docker compose run --rm app dotnet test`).
4. Ensure 100% scenario pass rate.
