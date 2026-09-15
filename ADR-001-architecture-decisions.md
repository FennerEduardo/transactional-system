# ADR 001: Architecture Decisions for Plataforma Transaccional Distribuida Event-Driven

## Status
Accepted

## Context
Project requiring structured implementation matching Gherkin specification.

## Decisions
- **Architecture Style**: CQRS + Event Sourcing (cqrs)
- **Primary Backend Language**: csharp
- **Backend Framework**: dotnet-aspnetcore (.NET 8.0 / 9.0 / 10.0)
- **ORM / Persistence**: entity-framework-core (Microsoft.EntityFrameworkCore 8.0/9.0/10.0)
- **Validation**: fluentvalidation (FluentValidation 11.9)
- **Authentication**: jwt (bcrypt cost factor 12, JWT TTL 3600s)
- **Backend Testing Framework**: xunit (xunit 2.7.0)

## Prohibited Layer Dependencies
Domain core must NOT import:
- `express`
- `@nestjs/common`
- `Microsoft.AspNetCore.*`
- `System.Data.SqlClient unparameterized queries`
