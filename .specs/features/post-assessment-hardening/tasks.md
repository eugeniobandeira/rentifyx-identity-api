# Tasks: Post-Assessment Hardening

Legend: `[P]` = safe to run in parallel with other `[P]` tasks in the same group.

## Group 1 — Test file split (do first, unblocks clean diffs later)

### T-01 — Split `LgpdEndpointTests.cs` by endpoint
- **What:** Extract `GetProfileEndpointTests.cs`, `DeleteAccountEndpointTests.cs`,
  `ExportDataEndpointTests.cs` from the existing shared file, mirroring the one-file-per-endpoint
  pattern already used for Auth (`LoginEndpointTests.cs`, `RegisterEndpointTests.cs`, etc.)
- **Where:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/`
- **Depends on:** nothing
- **Reuses:** existing `CustomWebApplicationFactory`, `LocalStackFixture` if referenced
- **Done when:** three new files exist, old `LgpdEndpointTests.cs` removed, all tests still pass
- **Tests:** no new test cases — pure reorganization; test count before/after must match
- **Gate:** `dotnet test` green, no reduction in test count

## Group 2 — Documentation & housekeeping (single PR, no code behavior change)

### T-02 [P] — Refresh CLAUDE.md ✅ DONE (2026-07-11)
- **What:** Update the stale claims: CI gates/OWASP/Trivy status (done, not planned), ADR count
  (8 written, not just template), IaC status (done, not "Week 6 TBD")
- **Where:** `CLAUDE.md`
- **Depends on:** nothing
- **Done when:** CLAUDE.md matches `.specs/project/STATE.md` + `ROADMAP.md` on all four points
- **Gate:** manual diff review against STATE.md/ROADMAP.md

### T-03 [P] — Commit `docs/api-contracts.md` ✅ DONE (2026-07-11)
- **What:** `git add docs/api-contracts.md` and include in the next commit
- **Where:** `docs/api-contracts.md`
- **Depends on:** nothing (content already verified consistent with `CookieExtensions.cs`)
- **Done when:** file is tracked, shows in `git status` as committed
- **Gate:** `git status` clean

### T-04 [P] — Remove stale coverage exclusions + untrack `.csproj.user`
- **What:** Remove `UserRepository.cs`/`EmailService.cs` exclusions from `coverlet.runsettings`
  (D-011 flagged these as stale); `git rm --cached` the tracked
  `RentifyxIdentity.Domain.csproj.user` file and confirm `.gitignore` has `*.csproj.user`
- **Where:** `coverlet.runsettings`, `.gitignore`, `02-src/03-Domain/RentifyxIdentity.Domain/RentifyxIdentity.Domain.csproj.user`
- **Depends on:** nothing
- **Done when:** coverage report includes both files, csproj.user no longer tracked
- **Gate:** `dotnet test` + coverage run still passes the 80% gate (these files are already
  well-tested per the assessment — removing the exclusion should not drop the gate, but confirm)

### T-05 [P] — Write `docs/guides/adding-a-new-feature.md` ✅ DONE (2026-07-11)
- **What:** Document the 7-step feature process from CLAUDE.md's "Adding a new feature" section,
  with a worked example pointing at the `register-user` feature as reference implementation
- **Where:** new file `docs/guides/adding-a-new-feature.md`
- **Depends on:** nothing
- **Done when:** guide exists, covers Domain → Contracts → Application → Infrastructure → IoC →
  API → Tests in order, links to real file paths in `register-user` as examples
- **Gate:** manual review — a new contributor should be able to follow it end-to-end

## Group 3 — Coverage polish (lowest priority, do anytime as filler)

### T-06 [P] — Raise `PasswordHasher` coverage (50% → target 90%+)
- **Where:** `02-src/05-Infrastructure/.../Services/PasswordHasher.cs` + its test file
- **Depends on:** nothing
- **Gate:** ReportGenerator shows improved method coverage for this class

### T-07 [P] — Raise `CorrelationIdMiddleware` coverage (57.1% → target 90%+)
- **Where:** `02-src/01-Api/.../Middlewares/CorrelationIdMiddleware.cs` + test
- **Depends on:** nothing
- **Gate:** same as T-06

### T-08 [P] — Raise `ErrorOrExtensions`/`OpenApiExtensions` coverage (67.8%/70.9% → target 90%+)
- **Where:** `02-src/01-Api/.../Extensions/ErrorOrExtensions.cs`, `OpenApiExtensions.cs` + tests
- **Depends on:** nothing
- **Gate:** same as T-06

## Not included here (blocked on discuss/design — see spec.md)

- R-03 (LGPD consent revoke) — needs a `discuss` pass to decide whether revoking consent
  suspends the account. Run `/tlc-spec-driven discuss` on this feature before creating its tasks.
- R-04 (TaxId KMS encryption) — needs a `design` pass (envelope encryption + HMAC blind index
  architecture). Run design before tasks, and sequence after `pf-pj-customer-support` lands.
