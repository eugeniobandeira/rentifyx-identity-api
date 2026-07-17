# Project State

## Last Updated

2026-07-12

## Current Work

v1.1.0 COMPLETE (2026-06-30) — login lockout, LGPD audit completeness, Aspire+LocalStack one-liner delivered. Tagged v1.1.0. Outbox (DEF-005) and TaxId KMS (DEF-007) deferred post-v1.1.0.

**`outbox-kafka-notifications` (DEF-005) is DONE as of 2026-07-17** — all 15 tasks (T0-T14) complete, all on branch `chore/remove-localstack-runtime` (**not yet merged/PR'd to `main`** — PR should be opened next now that Execute is finished). **T13 completed 2026-07-17**: `RegisterUserHandler`/`ForgotPasswordHandler` no longer call `IEmailService` — both construct their domain event (`UserRegistered`/`PasswordResetRequested`) and pass it as `extraEvents` to `repository.AddAsync`/`UpdateAsync`. T9's flagged gap was closed as a prerequisite: `UserRepository` now injects the real `IOutboxEntryFactory` instead of T7's generic placeholder, so the Outbox actually produces comms-api's `NotificationRequestedMessage` shape end-to-end. A real gap beyond T13's stated scope was found and fixed same session: 8 integration test files extracted verification/reset tokens via the now-dead `FakeEmailService` — repointed to a new event-capture on `FakeUserRepository` instead (27 tests were failing, now pass). Full non-Docker suite: Validators 51/51, Handlers 166/166, Integration 46/46 — Repositories (`Category=RequiresDocker`) not re-run this session, no Docker daemon available locally; not expected to regress (only `UserRepository`'s constructor gained one injected parameter, matching T7/T8's existing pattern). See `.specs/features/outbox-kafka-notifications/tasks.md` T13 for full implementation notes.

**T14 completed 2026-07-17** (remove `IEmailService`/`EmailService`/SES dead code): deleted `IEmailService`, `EmailService`, `EmailServiceTests`, `FakeEmailService`; removed their DI registrations (`AddScoped<IEmailService, EmailService>`, `AddAWSService<IAmazonSimpleEmailServiceV2>`) and the `Ses:FromAddress` config key from both `appsettings.json`/`appsettings.Production.json` and `ConfigurationKeys`; removed `AWSSDK.SimpleEmailV2` from `Directory.Packages.props` and both referencing `.csproj` files.
**Decision on `iac/terraform/modules/ses` (recorded, not silent):** kept, not deleted. It's not actually dead — `module "cognito"` (optional, `enable_cognito` flag) still consumes `module.ses.identity_arn`/`ses_identity` as its `from_email_address`/`source_arn` for Cognito's own email sending, a separate concern from the app's now-removed direct `IEmailService`. What *was* dead and got removed: the app's own IAM role no longer needs `ses:SendEmail`/`ses:SendRawEmail` (deleted the `SESAccess` statement from `modules/iam/main.tf`, plus the now-unused `ses_identity_arn` variable and its wiring in root `main.tf`'s `module "iam"` block). `terraform validate` passes.

**Feature complete.** Next: open a PR for `chore/remove-localstack-runtime` (10+ commits, never PR'd across this whole feature — flagged as a habit deviation earlier this session), then this repo's producer side of the cross-repo Kafka flow is ready. `rentifyx-communications-api`'s consumer side (E-03/E-04) has been live for a while; full end-to-end (`identity-api` publish → real Kafka on `rentifyx-platform` → `comms-api` consume) still needs `rentifyx-platform`'s `terraform apply`, intentionally deferred until both app repos are ready — see that repo's own STATE.md.

Sequence completed 2026-07-16 (T7-T12):
- T7: `IUserRepository`/`UserRepository` atomic transactional write (user item + N outbox items in one `TransactWriteItemsAsync`) — `entity.ClearDomainEvents()` only after success, verified with a real forced-rollback test (>400KB item).
- T8: `IOutboxRepository`/`OutboxRepository` — `GetPendingAsync`/`MarkPublishedAsync`/`MarkFailedAsync`/`IncrementRetryAsync` over `GSI_Outbox`.
- T9: `IOutboxEntryFactory`/`OutboxEntryFactory` — real per-event-type routing (`UserRegistered`/`PasswordResetRequested` → `notification-requested` topic, comms-api's exact `DispatchNotificationRequest` shape; other 4 events → generic `user-lifecycle-events` envelope). **Not yet wired into `UserRepository`** — T7's generic placeholder mapping is still what actually runs; T12/T13 didn't touch this either, so this wiring is still open and should happen as part of (or just before) T13.
- T10: `Confluent.Kafka` 2.15.0 package added.
- T11: `IKafkaProducerFactory`/`KafkaProducerFactory`, plus (flagged gap, fixed same day) added a local Kafka resource to this repo's own AppHost (had none before — `Aspire.Hosting.Kafka`, mirrors comms-api's AppHost).
- T12: `OutboxPublisher` `IHostedService` (poll/produce/mark-status loop), verified against real Testcontainers.Kafka + LocalStack DynamoDB.

Full identity-api suite (non-Docker): 263/263 passing as of 2026-07-17 (`dotnet test RentifyxIdentity.slnx --filter "Category!=RequiresDocker"`). Real bugs found and fixed along the way (all documented in `.specs/features/outbox-kafka-notifications/tasks.md`'s per-task "Implementation notes"): `LocalStackFixture` setting `RegionEndpoint` alongside `ServiceURL` broke every LocalStack-backed test project-wide (root-caused via an isolated console repro, not guessed); `OutboxPublisher`'s unconditional `Program.cs` registration broke all 46 `CustomWebApplicationFactory`-based endpoint tests (fixed by removing it from that factory, mirroring comms-api's `RemoveKafkaDependentHostedServices` pattern); T13's cutover broke the same 46 endpoint tests' token-extraction helper a second time (fixed by moving token capture from `FakeEmailService` to `FakeUserRepository`, see above).

This feature is the producer-side counterpart to `rentifyx-communications-api`'s `NotificationRequested` Kafka contract — full cross-repo integration (identity-api publishes → communications-api consumes → real Kafka on `rentifyx-platform`'s EKS) isn't fully testable until T14/T15 close out this feature (T13's cutover is done — the producer path is live) and platform's Kafka infra is actually `terraform apply`'d (user is intentionally holding that `apply` until this feature and comms-api's E-05 both finish — see `rentifyx-platform`'s STATE.md).

Post-v1.1.0 assessment (2026-07-11) produced two new feature specs:
`.specs/features/post-assessment-hardening/` (doc drift, coverage polish, test file split,
LGPD consent revoke, TaxId KMS) and `.specs/features/pf-pj-customer-support/` (see D-018 —
PJ/CNPJ was never modeled as a first-class concept beyond digit-length detection; not yet
implemented).

R-03 (LGPD granular consent revoke) is COMPLETE (2026-07-11) — went through discuss → design →
tasks → execute. All 7 tasks (T-01 to T-07) done: `UserEntity` grant/revoke mutations,
`GetConsent`/`UpdateConsent` handlers + endpoints (`GET`/`PUT /api/v1/users/me/consent`),
DynamoDB persistence, `docs/api-contracts.md` updated. Also done from the same feature's `tasks.md` (housekeeping): R-01 (CLAUDE.md refresh), R-02
(api-contracts.md committed), R-06 (adding-a-new-feature guide). Still open from `tasks.md`:
R-05 (remove stale coverlet exclusions + untrack `.csproj.user`), R-07 (split
`LgpdEndpointTests.cs` by endpoint), R-08 (coverage polish); and R-04 from the hardening spec
proper (TaxId KMS — needs its own design pass). `pf-pj-customer-support` not started.

## Decisions

| ID | Decision | Rationale | Date |
|---|---|---|---|
| D-001 | ErrorOr<T> over exceptions for handler results | Explicit error modeling without exception overhead; maps cleanly to HTTP status codes | 2026-06-21 |
| D-002 | TaxId (CPF/CNPJ) detected by digit count only, no mod-11 | Study project scope — length-only detection (11 digits = CPF, 14 = CNPJ) is sufficient; mod-11 algorithm removed from both VO and validator | 2026-06-24 |
| D-003 | DynamoDB single-table design | ADR-005; schema-less, pay-per-use, no migration overhead | 2026-06-21 |
| D-004 | Custom RS256 JWT for internal access tokens; Cognito for user-facing auth deferred to E-05 | ADR-006 hybrid model: identity-api issues short-lived RS256 JWTs (15 min) for service-to-service calls; Cognito handles MFA/social login for end users — not yet wired | 2026-06-28 |
| D-005 | Refresh tokens stored as HMAC-SHA256 hash | Raw token only transmitted over HTTPS, never persisted | 2026-06-21 |
| D-006 | Soft delete + PII anonymization for account erasure | LGPD Art. 18 VI — hard delete breaks audit trails | 2026-06-21 |
| D-007 | Everything in English | User preference — no Portuguese in code, docs, or specs | 2026-06-21 |
| D-008 | Enums always stored as string values in DynamoDB, never as integers | Readability in DB and avoid int/value drift bugs; applies to UserRole and UserStatus | 2026-06-21 |
| D-009 | `ct` as CancellationToken parameter name everywhere in own interfaces and implementations | Shorter, less noise. Applied to `IRepository<T>`, `IHandler<,>`, all handlers, endpoints, repositories, and fakes. External interfaces (e.g. `IExceptionHandler`) keep their declared name. | 2026-06-30 |
| D-010 | TaxId stored as plaintext for now | KMS encryption + HMAC blind index deferred to E-04 (DynamoDB wiring epic); acceptable for a study project in local/dev stage | 2026-06-24 |
| D-011 | Coverage gate excludes Example scaffold; Infrastructure stubs replaced in E-04 | Example files are living-pattern templates, not features. UserRepository/EmailService/TokenService stubs replaced with real AWS adapters in E-04 — coverage exclusions should be revisited. | 2026-06-28 |

## Blockers

_None active._

## Deferred Ideas

| ID | Idea | Deferred until |
|---|---|---|
| DEF-001 | Social login (OAuth — Google, Facebook) | Post-v1 |
| DEF-002 | MFA / 2FA | Post-v1 |
| DEF-003 | Granular RBAC beyond Owner/Renter/Admin | Post-v1 |
| DEF-004 | Rate limiting per-user lockout state (5 failed logins → 15-min lockout) | E-05 |
| DEF-005 | Domain event dispatch via Outbox pattern | E-05 |
| DEF-006 | LGPD export: consent records and login history | Confirm scope with team before E-05 |
| DEF-007 | TaxId KMS encryption + HMAC blind index for secure search | Post-v1.1.0 — skipped in v1.1.0 by design |

| D-012 | `UserRepository` uses `IDynamoDBContext` (high-level API), not `IAmazonDynamoDB` | Eliminates manual `Dictionary<string, AttributeValue>` construction; `SaveAsync`/`LoadAsync`/`DeleteAsync` are cleaner and less error-prone | 2026-06-30 |
| D-013 | `UserDynamoDbItem` GSI properties named in PascalCase with `[DynamoDBProperty]` for physical name | CA1707 forbids underscores in member names; `[DynamoDBProperty("GSI_Email_PK")]` preserves the DynamoDB attribute name | 2026-06-30 |
| D-014 | `ForgotPasswordHandler` delegates HMAC hashing to `ITokenService.HashToken()` | Eliminates duplicated HMAC-SHA256 logic and the security risk of a hardcoded `"dev-hmac-key"` fallback | 2026-06-30 |
| D-015 | `EmailService` validates `Ses:FromAddress` at construction time | Fail-fast pattern: invalid config surfaces at startup, not at the first email send | 2026-06-30 |
| D-016 | DynamoDB table requires SK as range key (`USER#{id}`) equal to PK | `[DynamoDBRangeKey("SK")]` on `UserDynamoDbItem` requires the table to define SK; enables future composite-key access patterns (e.g. audit log items on same table) | 2026-06-30 |
| D-017 | Login lockout state stored as `FailedLoginAttempts` (int) + `LockoutUntil` (DateTimeOffset?) on `UserEntity` | Co-locates lockout state with the user record; single `UpdateAsync` call; `LockoutUntilEpoch` (Unix seconds) mapped in `UserDynamoDbItem` for DynamoDB TTL auto-cleanup compatibility | 2026-06-30 |
| D-018 | PJ (CNPJ) support exists only at `TaxDocument` VO level (length-based detection, D-002) — no `CustomerType`, no company name, no legal representative anywhere in `RegisterUserRequest`/`UserEntity`. Confirmed by grep: zero references to `Cnpj`/`CompanyName`/`TaxDocumentType` outside the VO and DynamoDB mapping files. | Flagged during 2026-07-11 assessment when asked to verify PF/PJ coverage — a rental marketplace needs a named person of record even for business accounts (LGPD protects the natural person's data, not the CNPJ itself). New feature spec created: `pf-pj-customer-support`. | 2026-07-11 |
| D-019 | LGPD consent revoke (R-03) scope expanded from all-or-nothing to per-purpose (Essential, Marketing), via `discuss` on `post-assessment-hardening` feature | Revoking Essential suspends the account (reuses existing `Suspend()`/`UserStatus.Suspended` gating already enforced across Login/RefreshToken/ResetPassword/VerifyEmail); revoking Marketing only affects `rentifyx-communications-api` sends, no account impact. Revocation ≠ deletion — data retained, `DeleteAccount` stays the only anonymization path. Re-granting Essential reactivates the account. Existing users: Essential inherited from current `ConsentGivenAt`, Marketing defaults to not-granted (no retroactive assumption). Full details in `.specs/features/post-assessment-hardening/context.md`. | 2026-07-11 |
| D-020 | `rentifyx-communications-api` is a separate microservice owning marketing/comms sends; identity-api is only the consent source of truth | Surfaced during D-019 discussion — marketing consent has no local sender (`IEmailService` here only does transactional auth email). Cross-service notification of consent changes should ride the deferred Outbox (DEF-005) once it exists; until then that service must poll/query consent state. | 2026-07-11 |
| D-021 | R-03 design: keep `ConsentGivenAt` untouched (means "Essential granted at"); add `EssentialConsentRevokedAt`, `MarketingConsentGivenAt`, `MarketingConsentRevokedAt` to `UserEntity`/`UserDynamoDbItem`; "granted" = timestamp present, no separate bool | Avoids renaming a field used across 6+ files for no functional gain; matches existing `SetConsent` single-timestamp convention. Single `PUT /users/me/consent` with `{Purpose, Granted}` body instead of 4 separate endpoints. No Outbox/event dispatch to `rentifyx-communications-api` yet — deferred until DEF-005 ships. Full design: `.specs/features/post-assessment-hardening/design-consent-revoke.md` | 2026-07-11 |
| D-022 | Removed LocalStack from the runtime path entirely — `AppHost.cs`, `appsettings.*.json`, `InfrastructureDependencyInjection`, `SecretsManagerConfigurationProvider`, `docker-compose.yml`, and `init-localstack.sh` all deleted/stripped of LocalStack. Local dev now points at real AWS resources (DynamoDB `rentifyx-development-identity`, secret `rentifyx/identity/development`), provisioned via `terraform apply -var="environment=development"` before running and destroyed after. `AppHost.cs`'s hardcoded `RegionEndpoint.SAEast1` was also replaced with a configurable `AWS:Region` read (default `sa-east-1`), matching the convention already used in `SecretsManagerConfigurationProvider`. Repository integration tests (`03-tests/04-Repositories`) keep LocalStack via Testcontainers — untouched, since they don't need real AWS and this keeps CI free of cost/credentials. | Root cause of "Application terminated unexpectedly" on `dotnet run` via Aspire: commit `dd1c653` removed the LocalStack container from `AppHost.cs` (switching to target real AWS) but `appsettings.Development.json` still had `LocalStack:UseLocalStack=true` pointing at `localhost:4566`, and `SecretsManagerConfigurationProvider.Load()` runs synchronously during config load, before the host builds — a `HttpRequestException`/`SocketException` from the refused connection wasn't caught by the provider's existing `catch` clauses (only `AmazonServiceException` and friends), so it took the whole process down. User decided to fully commit to real AWS for local dev too rather than keep a two-track (LocalStack + real AWS) setup. | 2026-07-12 |

## Lessons Learned

| ID | Lesson | Context |
|---|---|---|
| L-001 | Never use `replace_all: true` on strings ≤ 3 characters — it corrupts unrelated identifiers (e.g., replacing "ct" rewrote `ValueObjects` → `ValueObjecancellationTokens`, `Conflict` → `ConflicancellationToken`). Always use targeted single-occurrence edits. | Happened during CancellationToken → ct rename across handler and repository files |
| L-002 | After adding a NuGet package, Debug builds may still fail with CS0234 due to stale NuGet cache. Run `dotnet clean` on the affected project before rebuilding. Release builds were unaffected. | Happened after adding `Microsoft.Extensions.Configuration.Abstractions` to Application project |
| L-003 | `LocalStack.Client.Extensions` 2.0.0 requires `AWSSDK.Core >= 4.0.0.15`. Pinning any AWSSDK package to v3.x causes a restore conflict. All AWSSDK packages must be on v4.x when using LocalStack.Client.Extensions 2.x. | Surfaced in E-04 when initial versions were 3.7.x |
| L-004 | `Aspire.Hosting.AWS` 13.x is CDK-based and not compatible with the standard Aspire hosting model. The correct Aspire-compatible package is 9.3.1. | Pin to 9.3.1; do not follow the latest NuGet version for this package. |
| L-005 | `Testcontainers.LocalStack` 4.x deprecates the parameterless `LocalStackBuilder()` ctor. With `TreatWarningsAsErrors=true`, use `new LocalStackBuilder("localstack/localstack:latest")` instead. | Surfaced in E-04 repository integration tests. |
| L-006 | Singletons that require secrets at construction time (e.g., `TokenService` reading `Jwt:PrivateKeyPem`) will crash integration tests unless a `FakeTokenService` is registered in `CustomWebApplicationFactory`. The real service must be explicitly overridden — DI does not auto-substitute. | Caused 7 integration test failures at the end of E-04 until `FakeTokenService` was added. |
| L-007 | `Agent` calls with `isolation: "worktree"` check out a new git worktree from the last **commit** — they do NOT see uncommitted changes sitting in the main working tree. Launching parallel worktree tasks that all depend on the same not-yet-committed prerequisite (e.g., three tasks all needing an uncommitted `UserEntity` change) causes each worktree to independently re-derive the missing piece, often diverging from the intended design. Commit the prerequisite first, or avoid `isolation: "worktree"` for tasks with a shared uncommitted dependency. | Happened launching T-02/T-03/T-05 of the LGPD consent feature in parallel right after T-01 (`UserEntity` changes) instead of committing T-01 first; required manual reconciliation of 3 worktrees' domain-layer edits. |
| L-008 | `dotnet build` via the CLI does NOT regenerate a `.resx`'s `Designer.cs` — `ResXFileCodeGenerator` is a Visual Studio single-file-generator convention, not an MSBuild target invoked by `dotnet build`/`dotnet test`. Adding a new resx key requires manually adding the matching property to `Designer.cs` in the same style as existing entries. | Discovered adding `CONSENT_PURPOSE_REQUIRED`/`CONSENT_PURPOSE_INVALID` to `ValidationMessageResource.resx` — first build failed with CS0117 until Designer.cs was hand-edited. |
| L-009 | All of `03-tests/05-Integration`'s `[Collection("Integration")]` test classes share ONE `CustomWebApplicationFactory` (one in-process app host), and the API's rate limiter (`RateLimitExtension.cs`) is a single global, non-partitioned fixed-window bucket (100 req/60s) — every test class's HTTP calls draw from that same budget for the whole test run. A new test class with enough real requests can push unrelated, already-passing tests in other classes over the limit (429). `IConfiguration` override via `ConfigureAppConfiguration` does NOT reach the limiter (root cause not diagnosed — not worth the time sunk). Re-registering `services.AddRateLimiter(...)` from the test project does not compile (`Microsoft.AspNetCore.RateLimiting` types aren't resolvable from a plain `Microsoft.NET.Sdk` test project even with an explicit `FrameworkReference`). Working fix: give a request-heavy new test class its own `IClassFixture<CustomWebApplicationFactory>` instead of joining the shared collection, AND add `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (two `WebApplicationFactory<Program>` instances starting concurrently crash host startup with "entry point exited without ever building an IHost"). | Surfaced adding `ConsentEndpointTests.cs` (T-06 of the LGPD consent feature) — broke 6 unrelated tests in `RegisterEndpointTests`/`VerifyEmailEndpointTests` until isolated. |
| L-010 | In this sandbox, `03-tests/04-Repositories` (`UserRepositoryTests`, Testcontainers + `LocalStack`) fails ALL 13 tests with `Amazon.DynamoDBv2.AmazonDynamoDBException: The security token included in the request is invalid` during `LocalStackFixture.CreateTableAsync()` — including the 10 pre-existing tests, not just new ones, so it's an environment issue, not a code regression. Confirmed a real ephemeral container IS created each time (~42s startup, consistent with genuine boot), so it's not a "container never started" problem — the AWS SDK call against the freshly-started container is what's rejected. Restarting the docker-compose stack clean (`docker compose down --remove-orphans && docker compose up -d --build`) did not fix it. Not fully root-caused (possibly global `AWS_*` env vars in the shell interfering with the SDK's credential resolution ahead of the explicit `BasicAWSCredentials("test","test")` in `LocalStackFixture.cs`) — diagnosis stopped at the user's request. | Discovered verifying repository tests for PR #26 (LGPD granular consent). Verify `UserRepositoryTests` on a different machine/CI before relying on a green run in this sandbox. |

## Preferences

- All output (code, docs, specs, comments) must be in English
- No hardcoded values in tests — always use constants (e.g., `TestConstants`) and Bogus builders (e.g., `RegisterUserRequestBuilder`)

## Feature Completion Log

| Feature | Tasks | Tests | Completed |
|---|---|---|---|
| register-user | T1–T18 (18/18) | 52 (14 validators + 32 handlers + 6 integration) | 2026-06-24 |
| verify-email | T-01–T-14 (14/14) | 16 (4 validators + 9 handlers + 3 integration) | 2026-06-27 |
| login | T-01–T-12 (12/12) | 17 (4 validators + 7 handlers + 3 integration + builder) | 2026-06-27 |
| refresh-token | T-01–T-08 (8/8) | 15 (4 validators + 9 handlers + 2 integration + builder) | 2026-06-27 |
| logout | T-01–T-08 (8/8) | 11 (4 validators + 5 handlers + 2 integration) | 2026-06-27 |
| password-reset | T-01–T-14 (14/14) | 23 (7 validators + 13 handlers + 3 integration) | 2026-06-27 |
| ci-gates | T-018–T-020 (3/3) | — (CI-only; 95.6% line coverage verified locally) | 2026-06-27 |
| lgpd-endpoints | all tasks | 28 (6 validators + 14 handlers + 8 integration) | 2026-06-27 |
| aws-integration (E-04) | T01–T18 (18/18) | 6 unit (TokenService + EmailService) + 8 repository integration (Testcontainers/LocalStack) | 2026-06-28 |
| e05-security-lgpd (E-05) | T-01–T-29 (29/29) | 5 security header integration + 5 audit service unit + 6 audit handler unit + 3 LGPD integration audit + 2 consent validator + 1 consent integration + handler refactors (Register/VerifyEmail/ResetPassword) | 2026-06-29 |
| e06-iac-production (E-06) | T-01–T-25 (25/25) | 6 Terraform modules + root + backend · 7 K8s manifests + 2 overlays · appsettings.Production.json · docs/slo.md · 3 C4 diagrams · docs/runbook.md · git tag v1.0.0 | 2026-06-29 |
| v1.1.0-hardening | login lockout (DEF-004) + LGPD audit completeness (DEF-006) + Aspire+LocalStack one-liner | 6 entity + 7 lockout handler + 2 repo integration + 1 audit handler + 3 export handler · FakeAuditLogService extended · git tag v1.1.0 | 2026-06-30 |
