# LGPD Consent Revoke — Context

**Gathered:** 2026-07-11
**Spec:** `.specs/features/post-assessment-hardening/spec.md` (R-03)
**Status:** Ready for design

---

## Feature Boundary

Users can view their current consent state and revoke/grant consent per purpose (essential,
marketing), satisfying LGPD Art. 8º §5º (consent must be revocable at any time). This replaces
R-03's original narrower framing ("view/revoke consent") — the discussion surfaced that
all-or-nothing consent isn't sufficient, so scope is now genuinely granular, per-purpose consent.

---

## Implementation Decisions

### Account effect of revocation

- Revoking **essential** consent suspends the account: `UserEntity.Suspend()` (already exists,
  sets `Status = UserStatus.Suspended`).
- `UserStatus.Suspended` already blocks `Login`, `RefreshToken`, `ResetPassword`, `VerifyEmail`
  (confirmed via grep across all four handlers) — no new gating logic needed, revocation just
  needs to flip the status and the existing checks do the rest.
- Revoking **marketing** consent has **no effect on account status** — it only stops
  `rentifyx-communications-api` (see below) from sending marketing communications to this user.

### Consent granularity

- Two purposes for the MVP: **Essential** (required — granted at registration, mandatory to use
  the platform) and **Marketing** (optional — communications/marketing sends).
- Structure should be extensible (e.g., a list/dictionary of purpose → granted/timestamp) so a
  third purpose can be added later without remodeling, but only these two are populated now.

### Marketing consent has no local sender

- `IEmailService` in this codebase only sends transactional auth email (verification, password
  reset) — there is no marketing-send capability here, and there won't be one.
- **New information surfaced during discussion:** marketing/communications sends are owned by a
  separate microservice, `rentifyx-communications-api` (see
  `reference_communications_api` memory). `rentifyx-identity-api` is the **source of truth** for
  the consent flag but does not act on it directly.
- Implication for design: revoking/granting marketing consent needs to be **observable by that
  other service** — most naturally via a domain event (`MarketingConsentRevoked` /
  `MarketingConsentGranted`) dispatched through the same Outbox mechanism already deferred as
  DEF-005. This creates a soft dependency: full cross-service enforcement of marketing consent
  requires DEF-005 (Outbox) to exist. Until Outbox ships, identity-api can still correctly store
  and expose the consent state via API — it just can't proactively notify
  `rentifyx-communications-api` of changes, so that service would need to poll/query instead.

### Already-collected data on revocation

- Revoking essential consent (→ suspend) does **not** trigger anonymization. Suspension and
  deletion remain distinct flows — data is retained under other legal bases (contractual/legal
  obligation) until the user explicitly requests erasure via the existing `DeleteAccount`
  (Art. 18 VI) flow, which already does soft delete + PII anonymization.

### Re-consent flow

- A suspended-by-consent-revocation account can self-service re-grant essential consent via an
  authenticated endpoint (e.g., `POST /api/v1/users/me/consent`), which reactivates the account
  (`Status → Active`) as part of the same operation — symmetric with revocation.

### Migration of existing users

- Existing users have a single `ConsentGivenAt` (undifferentiated acceptance at registration).
  Migrating to the granular model:
  - **Essential** consent is inherited from the existing `ConsentGivenAt` (they already agreed to
    use the platform — this is the same legal basis, just re-labeled).
  - **Marketing** consent defaults to **not granted** for all existing users — LGPD requires
    consent to be specific and informed per purpose; a generic historical acceptance cannot be
    assumed to cover marketing. Existing users must explicitly opt in to marketing going forward.

### Agent's Discretion

- Exact DynamoDB attribute shape for storing per-purpose consent (e.g., a map attribute vs. one
  attribute pair per purpose) — left to the design phase.
- Exact endpoint routes/verbs (`PATCH` vs `POST` vs `DELETE` for grant/revoke) — left to design,
  should follow the existing `IEndpoint`/`IHandler<TRequest,TResponse>` conventions.
- Whether grant/revoke is one endpoint with a `purpose` + `granted` body, or split into separate
  endpoints per purpose — left to design.

---

## Specific References

- `rentifyx-communications-api` is a real, separate microservice in the RentifyX ecosystem that
  owns marketing/communications sends — saved as a cross-session reference memory
  (`reference_communications_api.md`) since it's relevant beyond this feature.
- Existing precedent to follow: `UserEntity.Suspend()`, `UserStatus` gating already present in
  `LoginHandler`/`RefreshTokenHandler`/`ResetPasswordHandler`/`VerifyEmailHandler`; `DeleteAccount`
  flow (Art. 18 VI) as the existing soft-delete + anonymization pattern to explicitly NOT reuse
  here (revocation ≠ deletion).

---

## Deferred Ideas

- Full enforcement of marketing consent by `rentifyx-communications-api` depends on the Outbox
  pattern (DEF-005) to notify that service of consent changes reliably. Until then, that service
  would need to query identity-api's consent state directly rather than being pushed events —
  noted as a soft dependency, not blocking this feature's own implementation.
- A third consent purpose (e.g., third-party data sharing) was raised as an option earlier in the
  discussion but not selected for the MVP — structure should remain extensible for it.
