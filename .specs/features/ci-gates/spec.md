# CI Security Gates — Spec

## Goal

Harden the GitHub Actions CI pipeline with three additional gates that must pass on every PR before merging to `main`.

## Requirements

### T-018 — Coverage Gate (line coverage ≥ 80%)

| ID | Requirement |
|---|---|
| C-01 | `dotnet test` collects coverage via `coverlet.collector` (`--collect:"XPlat Code Coverage"`) using `coverlet.runsettings` |
| C-02 | `coverlet.runsettings` excludes: Example scaffold files, Infrastructure stubs (`UserRepository`, `EmailService`) not yet implemented until E-04, test helper assembly (`Tests.Common`), compiler/source-generated code |
| C-03 | `reportgenerator` merges all per-project Cobertura XMLs into a single `TextSummary` |
| C-04 | A bash step parses the `Summary.txt` and exits non-zero if line coverage < 80% — blocking the build |
| C-05 | The `Summary.txt` is uploaded as a GitHub Actions artifact (`coverage-report`, 30-day retention) for inspection |
| C-06 | Verified locally: after applying exclusions, coverage is **95.6% line / 77% branch** |

**Exclusion rationale:**
- `Example*` files — scaffold/living-pattern templates, not production features
- `UserRepository`, `EmailService` — stubs throwing `NotImplementedException` until DynamoDB wiring (E-04)
- `Tests.Common` — test helper assembly (builders, fakes, constants)

### T-019 — OWASP Dependency Check

| ID | Requirement |
|---|---|
| O-01 | New CI job `owasp-check` runs after `build-and-test` succeeds |
| O-02 | Uses `dependency-check/Dependency-Check_Action@main` to scan all NuGet packages against NVD |
| O-03 | Build fails if any dependency has CVSS score ≥ 7 (`--failOnCVSS 7`) |
| O-04 | NVD database is cached in `~/.dependency-check/data` keyed by `Directory.Packages.props` hash to avoid repeated full downloads |
| O-05 | HTML report uploaded as artifact (`owasp-report`, 30-day retention) |

### T-020 — Trivy Container Scan

| ID | Requirement |
|---|---|
| V-01 | New CI job `trivy-scan` runs after `build-and-test` succeeds (parallel with `owasp-check`) |
| V-02 | Builds the Docker image from the existing root `Dockerfile` |
| V-03 | Uses `aquasecurity/trivy-action@master` to scan the built image |
| V-04 | Scans both OS packages and library dependencies (`vuln-type: 'os,library'`) |
| V-05 | Fails on CRITICAL or HIGH severity findings (`severity: 'CRITICAL,HIGH'`) |
| V-06 | Only reports vulnerabilities with available fixes (`ignore-unfixed: true`) |

## Out of scope

- SARIF upload to GitHub Security tab (requires Advanced Security — not available on free repos)
- NVD API key (rate-limited without key; first run may be slow — acceptable for study project)
- Per-assembly coverage thresholds (overall line coverage is sufficient at this stage)
- Adjusting threshold per-feature (global 80% gate is correct for current state)
