# Architecture Overview

## Layer Structure

```
01-Api              → Endpoints, Extensions, OpenAPI config
02-Application      → Handlers, Validators, DTOs, Mappers
03-Domain           → Entities, Interfaces, Constants, Resources
04-IoC              → Dependency injection registrations
05-Infrastructure   → Repository implementations, persistence
```

## Dependency Flow

```
Api → Application → Domain ← Infrastructure
            ↑
           IoC (wires all layers)
```

- **Domain** has no outbound dependencies — it defines interfaces, not implementations.
- **Infrastructure** implements Domain interfaces (repositories).
- **Application** depends only on Domain interfaces, never on Infrastructure directly.
- **IoC** is the only layer that references all others.
- **Api** depends on Application (handlers) and IoC.

## Feature Organization

Each feature lives under `Features/<FeatureName>/Handlers/<Operation>/` in the Application layer and `Endpoints/<FeatureName>/` in the Api layer. All files for one operation are co-located.

## Test Projects

| Project | What it tests |
|---|---|
| Tests.Validators | FluentValidation rules in isolation |
| Tests.Handlers | Handler logic with mocked repositories |
| Tests.Repositories | Repository queries against a real in-memory DB |
| Tests.Integration | Full HTTP pipeline via WebApplicationFactory |
| Tests.Common | Shared builders used by the above |
