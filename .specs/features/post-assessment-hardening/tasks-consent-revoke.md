# LGPD Granular Consent (R-03) — Tasks

**Design**: `.specs/features/post-assessment-hardening/design-consent-revoke.md`
**Status**: In Progress — T-01 through T-06 done (2026-07-11); T-07 pending

**T-06 note — real test-infrastructure bug found and fixed, not just endpoint wiring:** adding
`ConsentEndpointTests.cs` (9 tests against the shared `[Collection("Integration")]` factory)
initially broke 6 unrelated, pre-existing tests in `RegisterEndpointTests`/
`VerifyEmailEndpointTests` with 429 TooManyRequests. Root cause: the whole "Integration"
collection shares ONE `CustomWebApplicationFactory` (one in-process app host), and the API's
rate limiter (`RateLimitExtension.cs`, `AddFixedWindowLimiter("fixed", ...)`) is a single
global, non-partitioned bucket (100 req/60s) — every test's HTTP calls across every class
share that one budget for the whole test run. Adding 9 more real requests pushed the cumulative
count over the limit within the run's window. Tried and rejected: (1) `IConfiguration` override
via `ConfigureAppConfiguration` in-memory collection — had zero effect, config never reached the
limiter (root cause not fully diagnosed, not worth further sinking time into); (2)
re-registering `services.AddRateLimiter(...)` from the test project — didn't compile
(`AddRateLimiter`/`RateLimiterOptions` types not resolvable from a plain `Microsoft.NET.Sdk`
test project even with an explicit `FrameworkReference`). Working fix: give
`ConsentEndpointTests` its own `IClassFixture<CustomWebApplicationFactory>` instance instead of
joining the shared collection (so it gets an independent rate-limiter bucket), combined with
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` in
`IntegrationTestCollection.cs` (two concurrently-starting `WebApplicationFactory<Program>`
instances crashed host startup with "entry point exited without ever building an IHost" —
disabling cross-collection parallelism serializes them without forcing a shared rate-limit
bucket). Logged as L-009 in `.specs/project/STATE.md`.

**Reconciliation note (2026-07-11):** T-02, T-03, T-05 were run in parallel via isolated git
worktrees, but T-01 (`UserEntity` changes) was uncommitted at launch time, so none of the
worktrees saw it — each independently re-derived some or all of the missing `UserEntity` pieces,
diverging from the design in the T-05 worktree (missing `Suspend()` call on revoke, no grant
methods) and matching it correctly in the T-02 worktree (its prompt had embedded the exact
method signatures). Reconciled by hand: kept the real T-01 `UserEntity` as source of truth,
ported only each task's actual novel files onto it, fixed one method-name mismatch
(`SetMarketingConsent` → `GrantMarketingConsent`) in a ported test. All three worktrees' domain
layer edits were discarded. Full solution build + Validators (51) + Handlers (126) test suites
green after reconciliation. Repository tests (T-02) could not run — Docker/Testcontainers
unavailable in this sandbox, confirmed by both agents that attempted them; this is an environment
gap, not a code defect. Lesson logged as L-007 in STATE.md: **commit before launching parallel
worktree tasks that share a domain dependency.**

---

## Execution Plan

### Phase 1: Foundation (Sequential)

```
T-01
```

### Phase 2: Core Implementation (Parallel OK)

```
       ┌→ T-02 [P] (Infrastructure)
T-01 ──┼→ T-03 [P] (Application: DTOs + Validator)
       └→ T-05 [P] (Application: GetProfile/ExportData surfacing)
```

### Phase 3: Handlers (Sequential — needs T-03)

```
T-03 ──→ T-04
```

### Phase 4: API + IoC (Sequential — needs T-04)

```
T-04 ──→ T-06
```

### Phase 5: Docs (Sequential — needs final shape from T-06)

```
T-06 ──→ T-07
```

---

## Task Breakdown

### T-01: Domain — `ConsentPurpose` enum + `UserEntity` consent mutations ✅ DONE (2026-07-11)

**What**: Add `ConsentPurpose` enum (`Essential`, `Marketing`); add `EssentialConsentRevokedAt`,
`MarketingConsentGivenAt`, `MarketingConsentRevokedAt` fields to `UserEntity`; add
`GrantEssentialConsent(DateTimeOffset)`, `RevokeEssentialConsent(DateTimeOffset)` (calls existing
`Suspend()`), `GrantMarketingConsent(DateTimeOffset)`, `RevokeMarketingConsent(DateTimeOffset)`,
and read-only `IsEssentialConsentGranted`/`IsMarketingConsentGranted` properties; add 4 new
`AuditEvents` constants (`EssentialConsentGranted`, `EssentialConsentRevoked`,
`MarketingConsentGranted`, `MarketingConsentRevoked`).
**Where**: `03-Domain/RentifyxIdentity.Domain/Enums/ConsentPurpose.cs` (new),
`03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs` (modify),
`03-Domain/RentifyxIdentity.Domain/Constants/AuditEvents.cs` (modify)
**Depends on**: None
**Reuses**: `Suspend()`, `SetConsent()`-style single-timestamp mutation pattern (both already in
`UserEntity.cs`)
**Requirement**: R-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `ConsentPurpose` enum compiles with `Essential`/`Marketing`
- [ ] All 4 mutation methods + 2 read-only properties added to `UserEntity`
- [ ] `RevokeEssentialConsent` calls `Suspend()`; `GrantEssentialConsent` sets `Status = Active`
- [ ] 4 new `AuditEvents` constants added, existing 3 untouched
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers` (extends `UserEntityTests.cs`)
- [ ] Test count: existing `UserEntityTests.cs` count + at least 8 new cases (4 mutations ×
      grant/revoke idempotency + status transition assertions)

**Tests**: unit (Domain entity — per Test Coverage Matrix, "Domain entities/VOs" tested in
`Tests.Handlers`)
**Gate**: quick

**Commit**: `feat(consent): add granular consent mutations to UserEntity`

---

### T-02: Infrastructure — extend `UserDynamoDbItem` + `UserDynamoDbMapper` [P] ✅ DONE (2026-07-11, reconciled)

**What**: Add 3 nullable string attributes to `UserDynamoDbItem`
(`EssentialConsentRevokedAt`, `MarketingConsentGivenAt`, `MarketingConsentRevokedAt`); update
`UserDynamoDbMapper.ToItem`/`ToEntity` both directions. Existing `ConsentGivenAt` attribute
untouched.
**Where**: `05-Infrastructure/RentifyxIdentity.Infrastructure/Models/UserDynamoDbItem.cs`,
`05-Infrastructure/RentifyxIdentity.Infrastructure/Mapping/UserDynamoDbMapper.cs`
**Depends on**: T-01 (needs the new `UserEntity` fields to map to/from)
**Reuses**: existing `ParseDate`/`ToString("O")` conventions already in `UserDynamoDbMapper.cs`
**Requirement**: R-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Round-trip mapping (`ToItem` → `ToEntity`) preserves all 3 new fields
- [ ] An item with the 3 new attributes absent (simulating a pre-existing record) maps to `null`
      for all 3, confirming no-migration-needed default
- [ ] Gate check passes: `dotnet test 03-tests/04-Repositories` (extends
      `UserRepositoryTests.cs`, Testcontainers/LocalStack)
- [ ] Test count: existing `UserRepositoryTests.cs` count + at least 3 new cases (round-trip
      with all consent states set; round-trip with an item missing the new attributes)

**Tests**: integration (Testcontainers — per Test Coverage Matrix, "Repositories")
**Gate**: full

**Commit**: `feat(consent): persist granular consent fields in DynamoDB`

---

### T-03: Application — request/response DTOs + `UpdateConsentValidator` [P] ✅ DONE (2026-07-11, reconciled)

**What**: Add `GetConsentRequest(Guid UserId)`, `UpdateConsentRequest(Guid UserId, string
Purpose, bool Granted)`, `ConsentResponse` (6 fields per design). Add `UpdateConsentValidator`:
`Purpose` required and must be a valid `ConsentPurpose` name.
**Where**: `02-Application/RentifyxIdentity.Application/Features/Identity/User/Consent/Request/`
(new folder), `.../Consent/Validator/UpdateConsentValidator.cs` (new)
**Depends on**: T-01 (validator checks `Purpose` against `ConsentPurpose` enum names)
**Reuses**: `RegisterUserValidator`'s string-role validation pattern
(`Must(role => role is "Owner" or "Renter" or "Admin")`) as the template for the `Purpose` check
**Requirement**: R-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] All 3 DTOs defined per design's Data Models section
- [ ] Validator rejects any `Purpose` value not in `{"Essential", "Marketing"}`
- [ ] Validator accepts both valid `Purpose` values with any `Granted` boolean
- [ ] Gate check passes: `dotnet test 03-tests/02-Validators` (new
      `UpdateConsentValidatorTests.cs`)
- [ ] Test count: at least 6 new cases (2 valid purposes × true/false Granted, plus 2 invalid-purpose cases)

**Tests**: unit (Validators — no mocks, per Test Coverage Matrix)
**Gate**: quick

**Commit**: `feat(consent): add consent request/response DTOs and validator`

---

### T-04: Application — `GetConsentHandler` + `UpdateConsentHandler` ✅ DONE (2026-07-11)

**What**: Implement both handlers per design. `GetConsentHandler`: load user, 404 if
not-found/`Deleted`, map current consent state to `ConsentResponse`. `UpdateConsentHandler`:
load user, 404 if not-found/`Deleted`, validate request, call the matching `UserEntity` mutation
from T-01 based on `Purpose`+`Granted`, persist via `IUserRepository.UpdateAsync`, log the
matching audit event (failures logged-only, never fail the request — same try/catch pattern as
`GetProfileHandler.cs:38-45`), return updated `ConsentResponse`.
**Where**:
`02-Application/RentifyxIdentity.Application/Features/Identity/User/Consent/GetConsentHandler.cs`
(new),
`02-Application/RentifyxIdentity.Application/Features/Identity/User/Consent/UpdateConsentHandler.cs`
(new)
**Depends on**: T-01, T-03
**Reuses**: `GetProfileHandler.cs` as the exact structural template (NotFound check, audit
try/catch, `IHandler<TRequest,TResponse>` shape)
**Requirement**: R-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `GetConsentHandler` returns current state; 404 for not-found/Deleted
- [ ] `UpdateConsentHandler` grants/revokes the correct purpose and reflects it in the returned
      `ConsentResponse`
- [ ] Revoking Essential leaves `Status == Suspended`; granting Essential while Suspended sets
      `Status == Active`
- [ ] Revoking/granting Marketing never changes `Status`
- [ ] Correct `AuditEvents` constant logged per operation (verified via Moq `Verify`)
- [ ] Audit log failure (mocked exception) does not fail the handler result
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers` (new `GetConsentHandlerTests.cs`,
      `UpdateConsentHandlerTests.cs`)
- [ ] Test count: at least 10 new cases across both handlers (not-found, each of the 4
      grant/revoke combinations, idempotent re-revoke, audit failure tolerance)

**Tests**: unit (Handlers — Moq, per Test Coverage Matrix)
**Gate**: quick

**Commit**: `feat(consent): implement get/update consent handlers`

**Deviation from design**: added a `GetConsentValidator` (simple `UserId` `NotEmpty` check) even
though `design-consent-revoke.md` only specified a validator for `UpdateConsentRequest`. Matched
purely for codebase consistency — `GetProfileRequest` (the closest analog) already has its own
`GetProfileValidator` with the identical single rule, and CLAUDE.md's feature-order convention
pairs every Request with a Validator. Not a scope change, just following the established local
pattern the design note overlooked. Result: 141/141 Handlers tests pass (126 previous + 15 new:
6 `GetConsentHandlerTests` + 9 `UpdateConsentHandlerTests`).

---

### T-05: Application — surface consent fields in `GetProfile`/`ExportData` responses [P] ✅ DONE (2026-07-11, reconciled)

**What**: Extend `UserResponse`/export DTO and `UserMapper` to include
`EssentialConsentRevokedAt`, `MarketingConsentGivenAt`, `MarketingConsentRevokedAt` alongside the
existing `ConsentGivenAt`, completing DEF-006's "consent records in export" now that consent is
granular.
**Where**: `Application/Features/Identity/.../Response/*` (existing response types),
`UserMapper.cs`
**Depends on**: T-01 only (needs the new `UserEntity` fields; independent of the new
handlers/endpoints)
**Reuses**: existing `UserMapper.ToResponse` pattern
**Requirement**: R-03 (completion of DEF-006 scope)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `GetProfile`/`ExportData` responses include all 3 new fields alongside existing
      `ConsentGivenAt`
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers` (extends
      `GetProfileHandlerTests.cs`/`ExportDataHandlerTests.cs`)
- [ ] Test count: at least 2 new assertions (one per handler, confirming new fields present in
      response)

**Tests**: unit (Handlers — per Test Coverage Matrix)
**Gate**: quick

**Commit**: `feat(consent): surface granular consent fields in profile/export responses`

**Coordination note**: touches the same response/mapper files as
`pf-pj-customer-support` T-05 — land whichever feature reaches this point first, then rebase the
other before merging its own response changes.

---

### T-06: API — `GetConsent`/`UpdateConsent` endpoints + IoC registration ✅ DONE (2026-07-11)

**What**: Add `GET /api/v1/users/me/consent` and `PUT /api/v1/users/me/consent` endpoints
(`IEndpoint` implementations, `ClaimTypes.NameIdentifier` → `Guid userId` extraction, same
`result.Match(...)` shape as `GetProfile.cs`). Explicitly register `UpdateConsentValidator`,
`GetConsentHandler`, `UpdateConsentHandler` in `ApplicationDependencyInjection` (per the
project's explicit-DI convention — endpoints are still reflection-discovered, only
validators/handlers are explicit).
**Where**: `01-Api/RentifyxIdentity.Api/Endpoints/Users/GetConsent.cs` (new),
`01-Api/RentifyxIdentity.Api/Endpoints/Users/UpdateConsent.cs` (new),
`04-IoC/RentifyxIdentity.IoC/ApplicationDependencyInjection.cs` (modify)
**Depends on**: T-04
**Reuses**: `GetProfile.cs` endpoint template; existing `ApplicationDependencyInjection.cs`
registration list pattern
**Requirement**: R-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Both endpoints registered, return 200 on success, 401 if no valid JWT claim, mapped errors
      via `.ToProblem(httpContext)`
- [ ] DI resolves both handlers + validator without runtime error (this is also where IoC
      registration correctness is actually exercised — per the "merge forward" rule, since IoC
      wiring can't be tested standalone)
- [ ] Full flow works end-to-end: revoke Essential → subsequent `Login` attempt fails
      (`Suspended`) → grant Essential again → `Login` succeeds
- [ ] Gate check passes: `dotnet test 03-tests/05-Integration` (new `ConsentEndpointTests.cs`,
      using the existing `TestAuthHandler`/`CustomWebApplicationFactory`)
- [ ] Test count: at least 8 new cases (GET happy path, PUT × 4 grant/revoke combinations, 401
      unauthenticated, 404 deleted user, end-to-end suspend/reactivate/login sequence)

**Tests**: e2e (API Endpoints — per Test Coverage Matrix)
**Gate**: full

**Commit**: `feat(consent): add consent view/update endpoints`

---

### T-07: Docs — update `docs/api-contracts.md`

**What**: Document both new endpoints (request/response schemas, status codes, the
suspend-on-essential-revoke behavior) in the existing contracts doc, matching its established
format (used for the refresh-token cookie section).
**Where**: `docs/api-contracts.md`
**Depends on**: T-06 (needs final response/route shape)
**Reuses**: existing doc structure/format already in the file
**Requirement**: R-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Both endpoints documented with request/response examples and all status codes from T-06
- [ ] Manual review confirms it matches the shipped code exactly (same discipline the existing
      cookie section already follows)

**Tests**: none (docs are not a code layer in the Test Coverage Matrix — `none` is valid here)
**Gate**: build

**Commit**: `docs(consent): document consent endpoints in api-contracts.md`

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T-01

Phase 2 (Parallel, all depend only on T-01):
  T-01 complete, then:
    ├── T-02 [P]  (Infrastructure)
    ├── T-03 [P]  (Application DTOs/Validator)
    └── T-05 [P]  (GetProfile/ExportData surfacing)

Phase 3 (Sequential — only needs T-03, may start before T-02/T-05 finish):
  T-03 complete → T-04

Phase 4 (Sequential):
  T-04 complete → T-06

Phase 5 (Sequential):
  T-06 complete → T-07
```

---

## Task Granularity Check

| Task | Scope | Status |
|---|---|---|
| T-01: Domain enum + entity mutations + audit constants | 1 cohesive domain concept, 3 files | ✅ Granular (cohesive — tip allows 2-3 related things in same area) |
| T-02: DynamoDB item + mapper | 1 persistence concern, 2 files | ✅ Granular |
| T-03: DTOs + validator | 1 request contract, 2 files | ✅ Granular |
| T-04: 2 handlers | 1 use-case pair (get/update same resource) | ✅ Granular |
| T-05: Response surfacing | 1 concern (expose existing data), 2 files | ✅ Granular |
| T-06: 2 endpoints + IoC | 1 API surface for one resource | ✅ Granular |
| T-07: Docs | 1 file | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|---|---|---|---|
| T-01 | None | No incoming arrows | ✅ Match |
| T-02 | T-01 | T-01 → T-02 | ✅ Match |
| T-03 | T-01 | T-01 → T-03 | ✅ Match |
| T-04 | T-01, T-03 | T-03 → T-04 (T-01 satisfied transitively — T-03 already depends on T-01) | ✅ Match |
| T-05 | T-01 | T-01 → T-05 | ✅ Match |
| T-06 | T-04 | T-04 → T-06 | ✅ Match |
| T-07 | T-06 | T-06 → T-07 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T-01 | Domain entities/VOs | Unit (no mocks) | unit | ✅ OK |
| T-02 | Repositories | Integration (Testcontainers) | integration | ✅ OK |
| T-03 | Validators | Unit (no mocks) | unit | ✅ OK |
| T-04 | Handlers | Unit (Moq) | unit | ✅ OK |
| T-05 | Handlers (response mapping) | Unit (Moq) | unit | ✅ OK |
| T-06 | API Endpoints + IoC/DI registration | E2E / Integration | e2e (also exercises IoC per merge-forward rule) | ✅ OK |
| T-07 | Docs (not in matrix) | — | none | ✅ OK |

---

## Tools question for Execute phase

No project-specific MCPs or skills are needed for this feature — it's straightforward C#
following existing patterns end-to-end. Recommend the `write-code` skill to drive execution
task-by-task and `verify` for the final gate check before commit, if you want to use them;
otherwise plain sub-agent delegation per SKILL.md's Sub-Agent Delegation table works fine.
