# C4 Level 2 — Container

Shows the deployable units that make up the RentifyX Identity system and how they communicate.

```mermaid
C4Container
    title Container Diagram — RentifyX Identity API

    Person(user, "RentifyX User", "Tenant, Owner, or Admin")
    System_Ext(platform, "RentifyX Microservices", "Validate JWT on each request")

    System_Boundary(identity_system, "RentifyX Identity") {

        Container(api, "Identity API", ".NET 10 Minimal API / EKS Pod", "Handles registration, authentication, token issuance, LGPD rights, and security hardening")

        ContainerDb(dynamo, "DynamoDB Table", "AWS DynamoDB (single-table)", "Stores user records (PK=USER#id), refresh tokens (TTL), and audit log entries (PK=AUDIT#userId#ts, TTL=90d)")

        Container(secrets_mgr, "Secrets Manager", "AWS Secrets Manager", "Holds JWT private key PEM, HMAC secret, and SES sender address — loaded at API startup")

        Container(ses, "Email Service", "AWS SES v2", "Delivers email verification and password reset messages")

        Container(kms, "KMS Key", "AWS KMS — deferred", "Symmetric key for TaxId at-rest encryption; provisioned but not yet used by the application")

        Container(cognito, "Cognito User Pool", "AWS Cognito — deferred", "RS256 user pool provisioned via Terraform; MFA and social login flows deferred to post-v1.0.0")
    }

    Rel(user, api, "HTTPS requests", "JSON / REST")
    Rel(api, platform, "issues JWT validated by", "RS256 Bearer")
    Rel(api, dynamo, "GetItem / PutItem / UpdateItem / Query", "AWS SDK v4 / HTTPS")
    Rel(api, secrets_mgr, "GetSecretValue at startup", "AWS SDK v4 / HTTPS")
    Rel(api, ses, "SendEmail", "SES v2 API / HTTPS")
    Rel(api, kms, "Encrypt / Decrypt TaxId — deferred", "AWS SDK v4 / HTTPS")
    Rel(api, cognito, "future auth flows — deferred", "AWS SDK v4 / HTTPS")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

## Data Model — DynamoDB Single-Table

| Item type | PK | SK | Notes |
|---|---|---|---|
| User | `USER#<id>` | `USER#<id>` | All user attributes; GSI on `Email` and `TaxId` |
| Refresh token | `REFRESH#<hash>` | `REFRESH#<hash>` | TTL set on creation (sliding window) |
| Audit log entry | `AUDIT#<userId>#<yyyyMMddHHmmss>_<guid>` | same as PK | TTL = 90 days from creation |
