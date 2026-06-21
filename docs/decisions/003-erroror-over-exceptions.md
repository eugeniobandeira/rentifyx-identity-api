# ADR-003: ErrorOr\<T\> over exceptions for control flow

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

.NET applications traditionally use exceptions to signal both unexpected failures and expected domain rule violations (e.g., "email already registered", "invalid CPF"). Using exceptions for expected conditions has several downsides: it is expensive (stack unwinding), it leaks implementation details when not caught correctly, and it makes the failure path invisible at the call site — callers must know which exceptions to catch.

## Options Considered

- **Option A — Exceptions for everything**: Simple, idiomatic C# for unexpected errors. But domain violations expressed as exceptions are invisible in method signatures, slow, and hard to test without exception-specific assertions.
- **Option B — Result\<T, Error\> custom type**: Explicit success/failure in the return type. Requires implementing the discriminated union from scratch.
- **Option C — ErrorOr\<T\> (NuGet package)**: A battle-tested discriminated union that integrates with the .NET ecosystem. Supports multiple errors per operation, maps cleanly to HTTP problem details, and requires zero boilerplate.
- **Option D — OneOf**: Similar to ErrorOr but less ergonomic for HTTP mapping.

## Decision

**Option C** — `ErrorOr<T>` (package version 2.0.1) as the universal return type for all `IHandler<TRequest, TResponse>` implementations.

Rules:
- All handlers return `Task<ErrorOr<TResponse>>`.
- Domain validation errors are returned as `Error` values, not thrown.
- Infrastructure exceptions (e.g., DynamoDB unavailable) are caught at the handler boundary and converted to `Error.Failure(...)`.
- Endpoints map results to HTTP using `result.Match(success => ..., errors => errors.ToProblem(httpContext))`.
- Unexpected exceptions that escape are caught by `GlobalExceptionHandler`, which returns a generic 500 with no stack trace.

## Consequences

**Easier:**
- Failure paths are explicit in method signatures — no hidden `catch` chains.
- Multiple domain errors can be accumulated and returned in one response.
- Unit testing happy and sad paths is symmetrical (no `Assert.Throws`).
- Consistent HTTP mapping via the `ToProblem` extension.

**Harder:**
- Developers unfamiliar with discriminated unions need a short onboarding.
- Infrastructure callers must convert exceptions to `Error` — this is a discipline enforced by code review.
