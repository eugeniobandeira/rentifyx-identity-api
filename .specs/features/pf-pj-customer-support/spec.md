# Feature Spec: PF/PJ Customer Support

## Status
Draft — gap identified during post-v1.1.0 assessment, not previously tracked in ROADMAP.md or STATE.md.

## Problem

RentifyX is a rental marketplace; both individual people (PF — pessoa física, CPF) and
companies (PJ — pessoa jurídica, CNPJ) must be able to register as owners or renters.

Current state, verified in code:

- `TaxDocument.Create()` (`02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/TaxDocument.cs`)
  already detects CPF (11 digits) vs CNPJ (14 digits) by length (D-002) and masks both
  correctly in `ToString()`.
- `TaxDocumentType` enum already has `Cpf` / `Cnpj`.
- **Nothing else in the system reads `TaxDocumentType`.** `RegisterUserRequest` is
  `(string Email, string TaxId, string Password, string Role, bool ConsentGiven)` — no name,
  no company name, no legal representative, no customer-type concept.
- `UserEntity` has no name/display-name field of any kind — PF and PJ users are structurally
  identical today beyond the tax document's digit count.

This means PJ registration technically "works" (a 14-digit string won't be rejected) but is
not a modeled, intentional capability — there's no way to know at registration time whether the
platform is dealing with a person or a company, no company legal name captured, and no natural
person of record for a PJ account (relevant for LGPD, since LGPD Art. 5º protects personal data
of natural persons — a company's CNPJ itself is not personal data, but its legal representative's
name/email/CPF is).

## Requirements

| ID | Requirement | Notes |
|---|---|---|
| R-01 | `RegisterUserRequest` gains an explicit `CustomerType` (`Individual` \| `Business`) field, not inferred from TaxId length | Inferring type from digit count is fragile and implicit; the client should declare intent, and the validator cross-checks it against the TaxId format |
| R-02 | PF registration requires `FullName`; PJ registration requires `CompanyLegalName` + `LegalRepresentativeName` | A PJ account still needs a natural person of record for LGPD/notification purposes |
| R-03 | Validator enforces TaxId format matches declared `CustomerType` (11 digits ↔ Individual, 14 digits ↔ Business) | Prevents mismatched submissions (e.g., CustomerType=Individual with a 14-digit TaxId) |
| R-04 | `UserEntity` persists `CustomerType`, `FullName` (PF) or `CompanyLegalName`/`LegalRepresentativeName` (PJ) | New DynamoDB attributes on `UserDynamoDbItem`; backward-compatible additive change |
| R-05 | `GetProfile` / `ExportData` responses surface the new fields appropriately per customer type | LGPD export must include the same personal data set already returned, now inclusive of representative info for PJ |
| R-06 | `DeleteAccount` anonymization (`UserEntity.Anonymize()`) clears the new name fields the same way it clears other PII today | Must not leave `FullName`/`LegalRepresentativeName` behind after erasure |
| R-07 | Existing users (no `CustomerType` on record) default to `Individual` at read time | Avoids a data migration; new field is nullable/defaulted in the DynamoDB mapper |

## Out of scope

- CNPJ mod-11 style checksum validation (D-002 already ruled this out for the study-project scope — length-only detection stays).
- Company-level entities beyond one legal representative (e.g., multiple authorized signers) — deferred, note as a Future Consideration if raised.
- Any billing/subscription distinction between PF and PJ accounts.

## Dependencies

- Should land before or alongside the TaxId KMS encryption work (post-assessment-hardening
  feature, item 4) since both touch `TaxDocument`/`UserDynamoDbItem` mapping — sequence to avoid
  rework: PF/PJ fields first, then encrypt `TaxId` at rest so the KMS change only has to handle
  one shape of the persisted item.
- No dependency on the LGPD consent view/revoke work, but both touch `ExportData`/`GetProfile`
  responses — coordinate carefully to avoid merge conflicts if executed in parallel.
