# ADR-006: Custom JWT for internal auth, Cognito for user-facing auth

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

The identity service must issue tokens for two distinct audiences:

1. **User-facing authentication** — browser/mobile clients logging in, social login (Google OAuth), MFA flows.
2. **Internal service-to-service authentication** — other RentifyX microservices calling the identity API or each other.

AWS Cognito handles OAuth 2.0 / OIDC flows natively and provides MFA, social identity providers, and hosted UI out of the box. However, Cognito adds latency and cost for high-frequency internal calls, and internal services do not need the full OAuth consent flow.

## Options Considered

- **Option A — Cognito for everything**: One auth system. But every internal service-to-service call must validate a Cognito token, adding ~50ms of latency per call and Cognito API costs at scale.
- **Option B — Custom JWT only**: Full control, zero external dependency for token validation. But we must implement MFA, social login, and user pool management from scratch — significant security risk and engineering effort.
- **Option C — Custom JWT for internal + Cognito for user-facing**: Hybrid model. Cognito handles user registration, MFA, and social login. The identity service also issues short-lived custom JWTs (signed with a KMS-managed key) for internal service authentication. Internal services validate the JWT signature locally — no Cognito round-trip.

## Decision

**Option C** — hybrid model.

| Token type | Issuer | Audience | TTL | Use case |
|---|---|---|---|---|
| Custom JWT (Access) | identity-api | Internal microservices | 15 min | Service-to-service calls |
| Custom JWT (Refresh) | identity-api | identity-api only | 7 days | Token renewal |
| Cognito ID token | AWS Cognito | Client apps | Configurable | User-facing login, MFA, social |

The identity service acts as the bridge: after Cognito validates a user's credentials, the identity service issues its own short-lived custom JWT carrying the internal `userId`, `role`, and `sub` claims. Downstream services only need to verify the JWT signature (via the public key from Secrets Manager) — no Cognito API call required.

The JWT signing key is an RSA-2048 key stored in AWS Secrets Manager, fetched at startup, and rotated without redeployment.

## Consequences

**Easier:**
- MFA, social login (Google), and user pool management delegated to Cognito — no custom implementation.
- Internal token validation is fast (local signature check, no external call).
- Key rotation is possible without code changes.

**Harder:**
- Two token types to reason about; middleware must distinguish Cognito tokens from custom JWTs.
- Integration tests must mock both Cognito (cognito-local) and the custom JWT issuer.
- When a user is suspended, the custom JWT remains valid until its 15-minute TTL expires. A token blacklist or shorter TTL may be needed for immediate revocation.
