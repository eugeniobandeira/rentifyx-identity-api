# Feature Spec: Post-Assessment Hardening

## Status
Draft — consolidates the prioritized next steps from the 2026-07-11 project assessment.

## Problem

A full-repo assessment on 2026-07-11 found the codebase architecturally sound (clean layering,
CI security gates wired, httpOnly refresh cookie implemented correctly) but flagged eight
concrete, actionable gaps — mostly documentation drift and a few tracked-but-unresolved LGPD/
security items. This spec turns those findings into requirements.

## Requirements

| ID | Requirement | Source | Notes |
|---|---|---|---|
| R-01 | Refresh `CLAUDE.md` from `.specs/project/STATE.md` and `ROADMAP.md` | Finding: CLAUDE.md says CI gates/OWASP/Trivy are "planned," ADRs "to be written," IaC "Week 6 TBD" — all false, already shipped | Pure doc sync, no code risk |
| R-02 | Commit `docs/api-contracts.md` | File exists, verified consistent with `CookieExtensions.cs`, currently untracked | Just `git add` + commit, no content changes needed unless PF/PJ fields (this spec's sibling feature) change response shapes later |
| R-03 | LGPD: add granular, per-purpose consent (Essential, Marketing) with view/revoke/re-grant, distinct from the existing "consent included in export" (DEF-006, already shipped in v1.1.0) | New gap, not previously tracked — LGPD Art. 8º §5º requires consent be revocable at any time; today `ConsentGiven`/`ConsentGivenAt` is write-once, all-or-nothing at registration. Scope confirmed via `discuss` on 2026-07-11 — see `context.md` | New endpoints, see design note below and `context.md` for full decision set |
| R-04 | Encrypt `TaxId` at rest via KMS + HMAC blind index for lookup (DEF-007/D-010) | Explicitly deferred in STATE.md D-010, "post-v1.1.0" | Coordinate with `pf-pj-customer-support` feature — land after PF/PJ fields so the DynamoDB item shape is stable first |
| R-05 | Remove stale `coverlet.runsettings` exclusions for `UserRepository.cs`/`EmailService.cs` (real implementations since E-04, D-011 flags this as "should be revisited"); untrack `RentifyxIdentity.Domain.csproj.user` | Housekeeping | Verify `.gitignore` covers `*.csproj.user` going forward |
| R-06 | Write `docs/guides/adding-a-new-feature.md` | Referenced in CLAUDE.md's structure section but the file doesn't exist | Should describe the 7-step feature process already documented in CLAUDE.md, with a worked example (e.g., point at `register-user` as the reference feature) |
| R-07 | Split `LgpdEndpointTests.cs` into per-endpoint integration test files (`GetProfileEndpointTests.cs`, `DeleteAccountEndpointTests.cs`, `ExportDataEndpointTests.cs`), matching the Auth endpoints' one-file-per-endpoint convention | Consistency + easier to extend when PF/PJ/consent-revoke change these endpoints' responses | Do this *before* R-03/pf-pj-customer-support touch these endpoints further, so new tests land in the right file from the start |
| R-08 | Raise coverage on `PasswordHasher` (50% method coverage), `CorrelationIdMiddleware` (57.1%), `ErrorOrExtensions` (67.8%), `OpenApiExtensions` (70.9%) | Coverage report 2026-06-27 | Lowest priority — overall gate (80%) already passes at 95.6%; this is quality polish, not a blocker |

## Design notes

**R-03 (consent revoke) — RESOLVED via `discuss`, see `context.md`.** Summary: two purposes
(Essential, Marketing) stored per-user; revoking Essential calls existing `UserEntity.Suspend()`
(reuses existing `UserStatus.Suspended` gating already enforced in Login/RefreshToken/
ResetPassword/VerifyEmail — no new gating logic needed); revoking Marketing has no account
effect, it only signals `rentifyx-communications-api` (a separate microservice — see
`reference_communications_api` memory) to stop sending marketing communications, ideally via the
deferred Outbox (DEF-005) once it exists. Data is NOT anonymized on revoke — that stays exclusive
to the existing `DeleteAccount` flow. Re-granting Essential consent reactivates the account
(`Status → Active`) in the same call. Existing users' Essential consent is inherited from their
current `ConsentGivenAt`; Marketing defaults to not-granted for everyone (no retroactive
assumption). This is now ready for a `design` pass (DynamoDB shape for per-purpose consent,
endpoint routes) — proceed to `tasks` once design is drafted.

**R-04 (TaxId KMS)** — ADR-needed: encrypt-at-rest via envelope encryption (KMS data key) changes
the DynamoDB attribute from plaintext string to ciphertext blob, and lookups by TaxId
(`GetByTaxIdAsync`, used for duplicate-registration checks) need an HMAC blind index (a second,
deterministic attribute) since ciphertext isn't queryable. This is genuinely architectural —
route through the `design` step, not straight to tasks.

## Ordering rationale

1. R-07 (split test files) — 30 min, zero risk, unblocks clean diffs for everything downstream that touches those endpoints.
2. R-01, R-02, R-05, R-06 — pure documentation/housekeeping, no code behavior change, can land as one PR.
3. `pf-pj-customer-support` feature (sibling spec) — lands next since R-04 depends on its DynamoDB shape being final.
4. R-03 (consent revoke) — needs a `discuss` pass first (see design note).
5. R-04 (TaxId KMS) — needs a `design` pass first (see design note), lands after PF/PJ.
6. R-08 (coverage polish) — anytime, lowest priority, good filler task.
