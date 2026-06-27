# RegisterUser Specification

## Problem Statement

New users cannot access RentifyX without an account. Registration is the entry point for every Owner listing an item and every Renter making a booking. The system must create accounts securely, prevent duplicates (LGPD Art. 46), validate Brazilian tax identity (CPF/CNPJ), and gate access behind email verification before any session can be issued.

## Goals

- [ ] A new user can create an account with email, TaxId (CPF or CNPJ), password, and role — receiving a 201 response with their profile.
- [ ] Duplicate email or TaxId registrations are rejected with a clear 409 before any data is written.
- [ ] A verification email containing a 24h single-use HMAC token is sent upon successful registration.
- [ ] The new account starts in `PendingVerification` status — no login is possible until email is verified.
- [ ] All input is validated with field-level error messages (422) before any business logic runs.

## Out of Scope

| Feature | Reason |
|---|---|
| Email verification flow (`VerifyEmail`) | Separate use case — own spec |
| Login / token issuance | User must verify email first; separate spec |
| Social / OAuth registration | Post-v1 |
| Admin-only account creation | Not in v1; all roles self-register |
| Captcha / bot protection | Post-v1 |
| Invitation-based registration | Post-v1 |

---

## User Stories

### P1: New user registers successfully ⭐ MVP

**User Story:** As a new user, I want to create a RentifyX account with my email, TaxId, password, and role so that I can start using the platform.

**Why P1:** Without registration, no user can access any feature of the platform.

**Acceptance Criteria:**

1. WHEN a POST request is sent to `/api/v1/auth/register` with valid `Email`, `TaxId`, `Password`, and `Role` THEN the system SHALL create a `UserEntity` with `Status = PendingVerification` and `CreatedAt = now (UTC)`.
2. WHEN registration succeeds THEN the system SHALL return HTTP 201 with a `UserResponse` body containing `Id`, `Email`, `Role`, `Status`, and `CreatedAt` (no `TaxId`, no `PasswordHash` in response).
3. WHEN registration succeeds THEN the system SHALL generate a HMAC-SHA256 verification token, store its hash on the user entity with a 24h expiry, and dispatch a verification email via `IEmailService`.
4. WHEN registration succeeds THEN the system SHALL persist the user via `IUserRepository.AddAsync` exactly once.
5. WHEN registration succeeds THEN `UserRegistered` domain event SHALL be raised with `UserId`, `Email`, `Role`, `OccurredAt`.

**Independent Test:** POST to `/api/v1/auth/register` with valid data → 201 + `UserResponse`; user exists in store with `Status = PendingVerification`; verification email triggered once.

---

### P1: Duplicate email is rejected ⭐ MVP

**User Story:** As the platform, I want to prevent two accounts from sharing the same email so that identity is unambiguous and LGPD Art. 46 is satisfied.

**Why P1:** Duplicate emails break authentication, data isolation, and LGPD compliance.

**Acceptance Criteria:**

1. WHEN a registration request arrives with an `Email` that already exists in the store THEN the system SHALL return HTTP 409 with error code `User.EmailAlreadyRegistered`.
2. WHEN a duplicate email is detected THEN the system SHALL NOT call `AddAsync` or send any email.
3. WHEN a duplicate email is detected THEN the system SHALL NOT reveal whether the TaxId or password was valid.

**Independent Test:** Register twice with the same email → second call returns 409; `AddAsync` called exactly once total.

---

### P1: Duplicate TaxId is rejected ⭐ MVP

**User Story:** As the platform, I want to prevent two accounts from sharing the same CPF or CNPJ so that each real-world person or company has exactly one identity.

**Why P1:** Duplicate TaxIds undermine identity trust and violate LGPD Art. 46.

**Acceptance Criteria:**

1. WHEN a registration request arrives with a `TaxId` that already exists in the store THEN the system SHALL return HTTP 409 with error code `User.TaxIdAlreadyRegistered`.
2. WHEN a duplicate TaxId is detected THEN the system SHALL NOT call `AddAsync` or send any email.

**Independent Test:** Register twice with same CPF but different emails → second call returns 409.

---

### P1: Invalid input is rejected with field-level errors ⭐ MVP

**User Story:** As a client application, I want structured validation errors per field so that I can display precise feedback to the user.

**Why P1:** Without field-level errors, the client cannot guide the user to fix their input.

**Acceptance Criteria:**

1. WHEN `Email` is missing or empty THEN the system SHALL return HTTP 422 with error on `Email` field and message from `EMAIL_REQUIRED`.
2. WHEN `Email` does not match RFC email format THEN the system SHALL return HTTP 422 with `EMAIL_INVALID_FORMAT`.
3. WHEN `Email` belongs to a known disposable domain THEN the system SHALL return HTTP 422 with `EMAIL_DISPOSABLE_DOMAIN`.
4. WHEN `Email` exceeds 320 characters THEN the system SHALL return HTTP 422 with `EMAIL_MAX_LENGTH`.
5. WHEN `TaxId` is missing or empty THEN the system SHALL return HTTP 422 with `TAXID_REQUIRED`.
6. WHEN `TaxId` fails CPF or CNPJ mod-11 validation (including all-same-digit sequences) THEN the system SHALL return HTTP 422 with `TAXID_INVALID_FORMAT`.
7. WHEN `Password` is missing or empty THEN the system SHALL return HTTP 422 with `PASSWORD_REQUIRED`.
8. WHEN `Password` is shorter than 12 characters THEN the system SHALL return HTTP 422 with `PASSWORD_MIN_LENGTH`.
9. WHEN `Password` is missing an uppercase letter, lowercase letter, digit, or symbol THEN the system SHALL return HTTP 422 with `PASSWORD_COMPLEXITY`.
10. WHEN `Password` exceeds 128 characters THEN the system SHALL return HTTP 422 with `PASSWORD_MAX_LENGTH`.
11. WHEN `Role` is missing or empty THEN the system SHALL return HTTP 422 with `ROLE_REQUIRED`.
12. WHEN `Role` is not one of `Owner`, `Renter`, `Admin` THEN the system SHALL return HTTP 422 with `ROLE_INVALID`.
13. WHEN multiple fields are invalid THEN the system SHALL return ALL field errors in a single 422 response.
14. WHEN any validation error occurs THEN the system SHALL NOT call any repository or service method.

**Independent Test:** Send request with empty body → 422 with errors on `Email`, `TaxId`, `Password`, `Role`; `AddAsync` never called.

---

### P2: Response body never exposes sensitive data

**User Story:** As a security reviewer, I want the registration response to never include raw TaxId, password hash, or token values so that PII and secrets are not leaked via the API.

**Why P2:** Foundational security requirement — not MVP-blocking but must ship before production.

**Acceptance Criteria:**

1. WHEN registration succeeds THEN the system SHALL NOT include `TaxId`, `PasswordHash`, `EmailVerificationTokenHash`, or `EmailVerificationTokenExpiry` in the response body.
2. WHEN `TaxDocument.ToString()` is called THEN it SHALL return the masked form (`***.***.***-**` for CPF).
3. WHEN `Password.ToString()` is called THEN it SHALL return `[REDACTED]`.

**Independent Test:** Inspect 201 response body — assert no field contains raw CPF digits or recognizable password hash.

---

### P2: Correlation ID is propagated

**User Story:** As an operator, I want every registration response to include a correlation ID so that I can trace a request across logs and support tickets.

**Why P2:** Operational requirement; does not affect the happy path.

**Acceptance Criteria:**

1. WHEN a registration request does not include `X-Correlation-Id` THEN the system SHALL generate a UUID and include it in the `X-Correlation-Id` response header.
2. WHEN a registration request includes a valid `X-Correlation-Id` THEN the system SHALL echo it in the response header.
3. WHEN registration fails (any error) THEN the correlation ID SHALL appear in the problem details `extensions.correlationId` field.

**Independent Test:** Send request without correlation header → response has `X-Correlation-Id`; resend with same ID → same ID echoed.

---

## Edge Cases

- WHEN `TaxId` is `111.111.111-11` (all-same-digit CPF) THEN system SHALL return 422 `TAXID_INVALID_FORMAT` (mod-11 rejects all-same-digit sequences).
- WHEN `Email` is `user@mailinator.com` (disposable domain) THEN system SHALL return 422 `EMAIL_DISPOSABLE_DOMAIN`.
- WHEN `Password` is exactly 12 characters and meets complexity THEN system SHALL accept it (boundary — valid).
- WHEN `Password` is exactly 128 characters THEN system SHALL accept it (boundary — valid).
- WHEN `Password` is 129 characters THEN system SHALL return 422 `PASSWORD_MAX_LENGTH`.
- WHEN `Role` is `owner` (lowercase) THEN system SHALL return 422 `ROLE_INVALID` (case-sensitive enum parsing).
- WHEN the email service fails after the user is persisted THEN system SHALL still return 201 — email delivery failure is non-blocking (event dispatch via Outbox in E-04 makes this resilient).
- WHEN a concurrent duplicate registration occurs (race condition on same email) THEN DynamoDB conditional write SHALL reject the second insert — the user sees a 500 until E-04 implements optimistic concurrency handling.
- WHEN `TaxId` is submitted with formatting characters (`529.982.247-25`) THEN system SHALL strip formatting before validation and storage.

---

## Requirement Traceability

| Requirement ID | Story | Status |
|---|---|---|
| REG-01 | P1: Successful registration — create user with PendingVerification | Pending |
| REG-02 | P1: Successful registration — return 201 + UserResponse (no sensitive fields) | Pending |
| REG-03 | P1: Successful registration — generate and store verification token hash (24h) | Pending |
| REG-04 | P1: Successful registration — dispatch verification email via IEmailService | Pending |
| REG-05 | P1: Successful registration — AddAsync called exactly once | Pending |
| REG-06 | P1: Successful registration — raise UserRegistered domain event | Pending |
| REG-07 | P1: Duplicate email — return 409 User.EmailAlreadyRegistered | Pending |
| REG-08 | P1: Duplicate email — no AddAsync or email sent | Pending |
| REG-09 | P1: Duplicate TaxId — return 409 User.TaxIdAlreadyRegistered | Pending |
| REG-10 | P1: Duplicate TaxId — no AddAsync or email sent | Pending |
| REG-11 | P1: Validation — Email required (422) | Pending |
| REG-12 | P1: Validation — Email RFC format (422) | Pending |
| REG-13 | P1: Validation — Email disposable domain (422) | Pending |
| REG-14 | P1: Validation — Email max 320 chars (422) | Pending |
| REG-15 | P1: Validation — TaxId required (422) | Pending |
| REG-16 | P1: Validation — TaxId mod-11 CPF/CNPJ (422) | Pending |
| REG-17 | P1: Validation — Password required (422) | Pending |
| REG-18 | P1: Validation — Password min 12 chars (422) | Pending |
| REG-19 | P1: Validation — Password complexity upper/lower/digit/symbol (422) | Pending |
| REG-20 | P1: Validation — Password max 128 chars (422) | Pending |
| REG-21 | P1: Validation — Role required (422) | Pending |
| REG-22 | P1: Validation — Role valid enum (422) | Pending |
| REG-23 | P1: Validation — all errors returned in single response | Pending |
| REG-24 | P1: Validation — no repo/service calls on validation failure | Pending |
| REG-25 | P2: Response never exposes TaxId, PasswordHash, or token fields | Pending |
| REG-26 | P2: TaxDocument.ToString() returns masked form | Pending |
| REG-27 | P2: Password.ToString() returns [REDACTED] | Pending |
| REG-28 | P2: Correlation ID generated if absent and echoed in response header | Pending |
| REG-29 | P2: Correlation ID appears in error problem details | Pending |

---

## Success Criteria

- [ ] POST `/api/v1/auth/register` with valid payload returns 201 + `UserResponse` in < 500ms (p99, excluding email dispatch)
- [ ] All 29 requirements above mapped to passing tests
- [ ] Zero fields containing raw TaxId digits or password hash in any response
- [ ] Coverage gate passes (≥ 80%) on `Tests.Validators` + `Tests.Handlers` for RegisterUser
- [ ] `dotnet build -c Release` passes with zero warnings (TreatWarningsAsErrors)
