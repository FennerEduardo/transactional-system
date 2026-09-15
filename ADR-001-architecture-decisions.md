# ADR 001: Architecture Decisions for Transactional System

## Status
Accepted

## Context
Project requiring structured implementation matching Gherkin specification.

## Decisions
- **Architecture Style**: Hexagonal Architecture (Ports & Adapters) (hexagonal)
- **Primary Backend Language**: csharp
- **Backend Framework**: dotnet-aspnetcore (.NET 8.0 / 9.0 / 10.0)
- **ORM / Persistence**: entity-framework-core (Microsoft.EntityFrameworkCore 8.0/9.0/10.0)
- **Validation**: zod (FluentValidation 11.9)
- **Authentication**: jwt (bcrypt cost factor 12, JWT TTL 3600s)
- **Backend Testing Framework**: reqnroll-xunit (NUnit 4.1)

## Prohibited Layer Dependencies
Domain core must NOT import:
- `express`
- `@nestjs/common`
- `prisma`
- `typeorm`
- `axios`
- `Microsoft.AspNetCore.*`
- `System.Data.SqlClient unparameterized queries`
