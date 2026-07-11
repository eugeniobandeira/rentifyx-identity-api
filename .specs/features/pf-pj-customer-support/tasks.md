# Tasks: PF/PJ Customer Support

Sequential — each layer depends on the one before it (Domain → Application → Infrastructure → API → Tests),
per CLAUDE.md's mandated feature order.

### T-01 — Add `CustomerType` enum + `UserEntity` name fields
- **What:** New `CustomerType` enum (`Individual`, `Business`) in `Domain/Enums/`. Add
  `CustomerType`, `FullName` (nullable), `CompanyLegalName` (nullable), `LegalRepresentativeName`
  (nullable) to `UserEntity`, set via `Create(...)`. Extend `Anonymize()` to null out the name
  fields alongside existing PII clearing.
- **Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Enums/CustomerType.cs`,
  `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs`
- **Depends on:** nothing
- **Reuses:** existing `Create`/`Anonymize` pattern already on `UserEntity`
- **Done when:** entity compiles, `Anonymize()` clears all four new-or-existing PII fields
- **Tests:** unit tests for `UserEntity.Create` with both customer types; `Anonymize()` clears
  name fields (extend `UserEntityTests.cs`)
- **Gate:** `dotnet test` on Domain-adjacent test project green

### T-02 — Extend `RegisterUserRequest` + validator (R-01, R-02, R-03)
- **What:** Add `CustomerType`, `FullName`, `CompanyLegalName`, `LegalRepresentativeName` to
  `RegisterUserRequest`. Validator rules: `CustomerType` required; `FullName` required when
  `Individual`; `CompanyLegalName` + `LegalRepresentativeName` required when `Business`; TaxId
  digit count must match declared `CustomerType` (11 ↔ Individual, 14 ↔ Business) — add a new
  validation message resource for the mismatch case.
- **Where:** `Application/Features/Identity/Auth/Register/Request/RegisterUserRequest.cs`,
  `.../Validator/RegisterUserValidator.cs`, `Domain/MessageResource/ValidationMessageResource.cs`
- **Depends on:** T-01
- **Reuses:** existing `RegisterUserValidatorTests.cs` pattern — add cases, don't restructure
- **Done when:** validator rejects mismatched type/TaxId combos and missing name fields per type
- **Tests:** extend `RegisterUserValidatorTests.cs` with all-valid/invalid combinations per
  CLAUDE.md's validator testing convention (both PF and PJ paths)
- **Gate:** `dotnet test` on Validators project green

### T-03 — Update `RegisterUserHandler` (R-04)
- **What:** Pass new fields through to `UserEntity.Create(...)`
- **Where:** `Application/Features/Identity/Auth/Register/RegisterUserHandler.cs`
- **Depends on:** T-01, T-02
- **Done when:** handler constructs entity with all new fields populated correctly per customer type
- **Tests:** extend `RegisterUserHandlerTests.cs` (Moq) with PF and PJ registration cases
- **Gate:** `dotnet test` on Handlers project green

### T-04 — Extend `UserDynamoDbItem` + mapper (R-04, R-07)
- **What:** Add `CustomerType` (string enum, per D-008), `FullName`, `CompanyLegalName`,
  `LegalRepresentativeName` attributes to `UserDynamoDbItem`; update `UserDynamoDbMapper` both
  directions. Default `CustomerType` to `Individual` when reading an existing record with the
  field absent (R-07 — no migration needed).
- **Where:** `02-src/05-Infrastructure/.../Models/UserDynamoDbItem.cs`,
  `02-src/05-Infrastructure/.../Mapping/UserDynamoDbMapper.cs`
- **Depends on:** T-01
- **Done when:** round-trip mapping preserves new fields; old items without the field map to `Individual`
- **Tests:** extend `UserRepositoryTests.cs` (Testcontainers/LocalStack) — one case per customer
  type, plus one case reading a pre-existing item shape without `CustomerType` set
- **Gate:** `dotnet test` on Repositories project green (Testcontainers)

### T-05 — Surface new fields in `GetProfile` / `ExportData` responses (R-05)
- **What:** Add the new fields to `UserResponse`/export DTOs and `UserMapper`, respecting
  customer type (don't show `CompanyLegalName` for a PF user, etc.)
- **Where:** `Application/Features/Identity/.../Response/*`, `UserMapper.cs`
- **Depends on:** T-01, T-04
- **Done when:** `GetProfile`/`ExportData` responses include the right fields per customer type
- **Tests:** extend `GetProfileEndpointTests.cs`/`ExportDataEndpointTests.cs` (created in
  `post-assessment-hardening` T-01 — land this task after that split, not before, to avoid
  editing the old shared file)
- **Gate:** `dotnet test` on Integration project green

### T-06 — Update `docs/api-contracts.md` and Bogus builders
- **What:** Document the new request/response fields; update `RegisterUserRequestBuilder`
  (Tests.Common) to generate valid PF and PJ payloads via Bogus, per the "no hardcoded values in
  tests" preference in STATE.md
- **Where:** `docs/api-contracts.md`, `03-tests/01-Common/.../Builders/RegisterUserRequestBuilder.cs`
- **Depends on:** T-02
- **Done when:** builder can produce both customer types; docs match the validator rules from T-02
- **Gate:** manual review + existing tests still compile against the updated builder

## Ordering
T-01 → T-02 → T-03 → T-04 → T-05 → T-06 (linear; each layer needs the previous one's shape finalized).
T-06's doc update can start as soon as T-02 lands, in parallel with T-03–T-05.
