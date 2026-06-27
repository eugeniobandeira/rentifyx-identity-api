# ADR-001: Secrets Manager over appsettings for all sensitive config

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

The application requires sensitive configuration values such as the JWT signing key, the Cognito client secret, and DynamoDB endpoint overrides. The default .NET pattern stores these in `appsettings.json` or environment variables, both of which risk accidental exposure — via committed files, container environment dumps, or log leakage — and violate OWASP A02 (Cryptographic Failures).

## Options Considered

- **Option A — appsettings / environment variables**: Simple, zero-cost, supported natively by .NET `IConfiguration`. But secrets can land in Git, CI logs, or container inspect output.
- **Option B — AWS Secrets Manager via `SecretsManagerConfigurationProvider`**: Secrets never leave AWS. Rotation is supported natively. Access is audited via CloudTrail. Adds an AWS API call at startup and a 5-minute in-process cache to avoid rate limits.
- **Option C — HashiCorp Vault**: More complex to operate; not aligned with our AWS-native infrastructure choice.

## Decision

**Option B** — AWS Secrets Manager.

A custom `SecretsManagerConfigurationProvider` loads all secrets into `IConfiguration` at startup. A 5-minute TTL cache is applied to avoid hitting Secrets Manager rate limits on every configuration read. The latest secret version is fetched on each application restart, enabling zero-downtime key rotation.

For local development, LocalStack emulates Secrets Manager so no real AWS credentials are needed.

## Consequences

**Easier:**
- Zero secrets in source code, CI logs, or container environment variables.
- Key rotation requires only a Secrets Manager update — no redeployment.
- Access audited via AWS CloudTrail.

**Harder:**
- Application startup fails fast if Secrets Manager is unreachable (by design).
- Local setup requires LocalStack to be running (via .NET Aspire AppHost).
- Integration tests must seed LocalStack with the expected secrets before the app boots.
