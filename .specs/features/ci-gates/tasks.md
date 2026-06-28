# CI Security Gates — Task Breakdown

## Status Legend

| Symbol | Meaning |
|---|---|
| ⬜ | Pending |
| ✅ | Complete |

## Tasks

| # | Layer | What | Status |
|---|---|---|---|
| T-018 | CI | `coverlet.runsettings` + coverage collection + 80% gate in `ci.yml` | ✅ |
| T-019 | CI | OWASP dependency-check job in `ci.yml` | ✅ |
| T-020 | CI | Trivy container scan job in `ci.yml` | ✅ |

## Dependencies

```
T-018 → (must pass before T-019 and T-020 are meaningful)
T-019 ∥ T-020 (independent CI jobs, both run after build-and-test)
```
