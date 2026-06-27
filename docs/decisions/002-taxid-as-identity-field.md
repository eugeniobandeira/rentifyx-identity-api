# ADR-002: TaxId (CPF or CNPJ) as identity field (LGPD data minimization)

- **Date:** 2026-06-21
- **Status:** Accepted
- **Supersedes:** Initial draft that used CPF only

## Context

RentifyX operates in Brazil and must comply with the Lei Geral de Proteção de Dados (LGPD). Users need a stable, government-issued identifier to prevent duplicate accounts and enable KYC checks required by BACEN.

RentifyX has two owner archetypes:
- **Individuals** (natural persons) — identified by **CPF** (Cadastro de Pessoas Físicas, 11 digits).
- **Companies** (legal entities — property managers, real estate firms, corporate landlords) — identified by **CNPJ** (Cadastro Nacional da Pessoa Jurídica, 14 digits).

Renters are almost always individuals (CPF), but the system should not prevent a company from renting either. Accepting only CPF would exclude all legal-entity owners from the platform.

Both CPF and CNPJ are sensitive PII/business data under LGPD Article 5 and must be collected only when strictly necessary (data minimization, Article 6 VII), protected in transit and at rest (Article 46), and subject to the data subject's rights (Articles 17–22).

## Options Considered

- **Option A — Email only**: No tax document. Insufficient for BACEN KYC/AML requirements and cannot prevent duplicate accounts across email changes.
- **Option B — CPF only**: Works for individuals but blocks companies (CNPJ) from registering as owners.
- **Option C — TaxId (CPF or CNPJ) + Email**: A single `TaxId` value object that accepts either format, validates the correct algorithm for each, and masks output. Covers individuals and legal entities. Satisfies BACEN KYC.
- **Option D — Separate CPF and CNPJ fields**: Two optional nullable fields. More complex validation (exactly one must be present); doubles the GSI count in DynamoDB.

## Decision

**Option C** — `TaxId` value object that encapsulates either a CPF or CNPJ.

```
TaxDocument (value object)
  ├── Value    : string  (raw digits, never stored in plaintext)
  ├── Type     : TaxDocumentType  (Cpf | Cnpj)
  └── ToString(): masked — CPF → "***.***.***-**"
                           CNPJ → "**.***.***/****.** "
```

Type is inferred from length at construction time:
- 11 digits → CPF → mod-11 digit-verification algorithm
- 14 digits → CNPJ → mod-11 CNPJ digit-verification algorithm
- Anything else → domain validation error

Storage and lookup:
1. **Encryption at rest** — The raw value is encrypted via AWS KMS before being written to DynamoDB (LGPD Article 46).
2. **GSI key** — `TAXDOC#{HMAC-SHA256(rawValue, kmsKeyId)}` — deterministic hash used for deduplication lookup; no plaintext in the index.
3. **DynamoDB attribute** — `TaxDocEncrypted` (KMS ciphertext) + `TaxDocType` (`CPF` | `CNPJ`) + `TaxDocHmac` (GSI key).
4. **LGPD erasure** — On account deletion, `TaxDocEncrypted` is deleted and `TaxDocHmac` is replaced with a random UUID so the GSI entry becomes unreachable.

## Consequences

**Easier:**
- Both individuals (CPF) and companies (CNPJ) can register — no platform exclusion.
- Single value object, single GSI, single KMS encryption path.
- Masking and validation logic is self-contained in the domain with no leakage to upper layers.

**Harder:**
- The validator must detect the document type before running the correct algorithm — a `TaxDocumentType` discriminator is stored alongside the encrypted value so reads don't need to decrypt to know the type.
- Teams must never log the raw `TaxId` value (enforced by `TaxDocument.ToString()` masking and the SonarAnalyzer rule against logging sensitive fields).
- CNPJ validation is slightly more complex than CPF (two separate mod-11 passes) — covered by dedicated unit tests.
