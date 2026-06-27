# RentifyX — Identity API

**Vision:** Production-grade identity microservice for RentifyX, a marketplace where people can rent anything — from real estate to equipment, tools, and vehicles.
**For:** Platform users (Owners listing items for rent, Renters browsing and booking, Admins managing the platform)
**Solves:** Secure, LGPD-compliant user identity — registration, authentication, authorization, and account lifecycle — as the foundational trust layer for all RentifyX services.

## Goals

- Ship a production-ready Identity API with zero critical OWASP vulnerabilities and full LGPD Art. 18 compliance
- Achieve ≥80% test coverage across unit, integration, and end-to-end tests
- Deliver all auth flows (register → verify → login → refresh → logout → password reset) with JWT + Cognito by end of Week 4 (E-04)
- Make the service observable and operable from day one (structured logs, OTel traces, health checks, Scalar UI)

## Tech Stack

**Core:**

- Framework: .NET 10 — Minimal APIs
- Language: C# (latest)
- Database: AWS DynamoDB (single-table design, LocalStack locally)

**Key dependencies:**

- ErrorOr 2.0.1 — result type for all handlers
- FluentValidation 12.1.1 — request validation
- AWS Cognito — JWT issuance and key management
- AWS SES — transactional email (verification, password reset)
- AWS Secrets Manager + KMS — secrets and CPF/CNPJ encryption at rest
- .NET Aspire — local orchestration, OTel, health checks
- Serilog — structured logging with correlation ID enrichment

## Scope

**v1 includes:**

- User registration with email verification (24h HMAC token)
- Login → Access JWT (15 min) + Refresh token (7d, one-time use, stored as hash)
- Token refresh with rotation and logout with revocation
- Password reset via signed email link (1h HMAC token)
- LGPD Art. 18: profile access (`GET /me`), account erasure with PII anonymization (`DELETE /me`), data export (`GET /me/data-export`)
- Three roles: `Owner`, `Renter`, `Admin`
- TaxId (CPF/CNPJ) as a unique identity field with mod-11 validation and KMS encryption at rest
- Rate limiting: 5 failed logins → 15-min lockout
- DevSecOps: gitleaks, Trivy, OWASP ZAP, coverage gate ≥80%, GitHub Actions CI on PRs

**Explicitly out of scope:**

- Social login (OAuth — Google, Facebook, etc.)
- MFA / 2FA
- Multi-tenancy
- Granular permissions beyond the three roles (`Owner`, `Renter`, `Admin`)
- Subscription or billing identity
- Admin panel UI

## Constraints

- Timeline: 28-day plan across 6 epics (E-01 → E-06)
- Technical: AWS-native (no EF Core, no relational DB); all secrets via Secrets Manager — never in code or env vars
- Compliance: LGPD (Brazil), OWASP Top 10, BACEN guidelines
- Coverage gate: ≥80% enforced in CI before merge
