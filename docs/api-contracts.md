# RentifyX Identity API — Frontend Integration Contracts

Base URL: `http://localhost:{port}/api/v1` (local) | `https://{host}/api/v1` (production)

---

## Configuration

### CORS
| Setting | Value |
|---|---|
| Allowed Origins | `http://localhost:3000` |
| Allowed Methods | All |
| Allowed Headers | All |
| Allow Credentials | Yes |
| Exposed Headers | `X-Correlation-Id` |
| Preflight Max Age | 10 minutes |

### Rate Limiting
| Setting | Value |
|---|---|
| Limit | 100 requests per 60 seconds |
| Queue | 0 |
| Rejection status | `429 Too Many Requests` |

### JWT
| Setting | Value |
|---|---|
| Issuer | `rentifyx-identity` |
| Audience | `rentifyx-services` |
| Algorithm | RS256 |
| Access token lifetime | 15 minutes |
| Refresh token lifetime | 30 days |

### Refresh Token Cookie

The refresh token is **never** exposed to JavaScript. It is set by the server as an `httpOnly` cookie on `/auth/login` and `/auth/refresh`, and cleared on `/auth/logout`.

| Attribute | Value |
|---|---|
| Name | `refreshToken` |
| `HttpOnly` | Yes |
| `Secure` | Yes (over HTTPS; omitted automatically over local HTTP) |
| `SameSite` | `Strict` |
| `Path` | `/api/v1/auth` (only sent to auth endpoints) |
| Max-Age | 30 days |

**Frontend requirement:** every request to `/api/v1/auth/*` must be made with `credentials: 'include'` (fetch) or `withCredentials: true` (axios), or the browser will not attach the cookie. Because `SameSite=Strict` and `Path` are scoped to `/auth`, no CSRF token is required for these endpoints, but the frontend origin must match `Cors:AllowedOrigins` exactly.

---

## Request Headers

| Header | Required | Description |
|---|---|---|
| `Authorization` | Yes (authenticated routes) | `Bearer {accessToken}` |
| `Content-Type` | Yes (POST/PUT) | `application/json` |
| `X-Correlation-Id` | No | Alphanumeric + hyphen, max 64 chars. Auto-generated if omitted. Always echoed in the response. |

---

## Response Format

### Success

Endpoints return `200`, `201`, or `204` depending on the operation. Body is JSON or empty.

### Validation Error — `422 Unprocessable Entity`

```json
{
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "Email": ["Email format is invalid."],
    "Password": ["Password must be at least 12 characters."]
  },
  "extensions": {
    "correlationId": "string | null"
  }
}
```

### Business / Server Error — `400 | 401 | 404 | 409 | 429 | 500`

```json
{
  "title": "Error description.",
  "status": 401,
  "extensions": {
    "correlationId": "string | null"
  }
}
```

### Security Headers (all responses)

| Header | Value |
|---|---|
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

---

## Status Code Reference

| Code | Meaning |
|---|---|
| `200` | OK — response body present |
| `201` | Created — resource created, body present |
| `204` | No Content — success, no body |
| `400` | Bad Request — business logic error |
| `401` | Unauthorized — missing/invalid token or wrong credentials |
| `404` | Not Found |
| `409` | Conflict — duplicate resource |
| `422` | Unprocessable Entity — validation errors |
| `429` | Too Many Requests — rate limit exceeded |
| `500` | Internal Server Error |

---

## Shared Types

### `UserResponse`
```json
{
  "id": "uuid",
  "email": "string",
  "role": "Owner | Renter | Admin",
  "status": "PendingVerification | Active | Suspended | Deleted",
  "createdAt": "ISO 8601 datetime with offset",
  "essentialConsentGranted": true,
  "essentialConsentGivenAt": "ISO 8601 datetime with offset | null",
  "essentialConsentRevokedAt": "ISO 8601 datetime with offset | null",
  "marketingConsentGranted": false,
  "marketingConsentGivenAt": "ISO 8601 datetime with offset | null",
  "marketingConsentRevokedAt": "ISO 8601 datetime with offset | null"
}
```

### `ConsentResponse`
```json
{
  "essentialGranted": true,
  "essentialGrantedAt": "ISO 8601 datetime with offset | null",
  "essentialRevokedAt": "ISO 8601 datetime with offset | null",
  "marketingGranted": false,
  "marketingGrantedAt": "ISO 8601 datetime with offset | null",
  "marketingRevokedAt": "ISO 8601 datetime with offset | null"
}
```
Returned by both consent endpoints. Essential consent is granted automatically at registration;
revoking it suspends the account (see [`PUT /api/v1/users/me/consent`](#put-apiv1usersmeconsent)).

### `AuthTokenResponse`
```json
{
  "accessToken": "string (JWT)",
  "user": { /* UserResponse */ }
}
```
The refresh token is **not** in this body — it arrives via the `refreshToken` `Set-Cookie` header. See [Refresh Token Cookie](#refresh-token-cookie).

### `AuditLogEntryRecord`
```json
{
  "eventType": "string",
  "occurredAt": "ISO 8601 datetime with offset"
}
```

---

## Endpoints

### Health

#### `GET /health`
Check if the API is running.

- Auth: No
- Response `200 OK`

---

### Auth — `POST /api/v1/auth/*`

#### `POST /api/v1/auth/register`
Create a new user account.

- Auth: No
- Response: `201 Created` → `UserResponse`

**Request body**
```json
{
  "email": "string",
  "taxId": "string",
  "password": "string",
  "role": "Owner | Renter | Admin",
  "consentGiven": true
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format · max 320 chars · no disposable domain (mailinator, guerrillamail, tempmail, throwam, yopmail) |
| `taxId` | Required |
| `password` | Required · 12–128 chars · must have uppercase, lowercase, digit, and symbol (`!@#$%^&*()-_=+[]{}|;:,.<>?`) |
| `role` | Required · one of `Owner`, `Renter`, `Admin` |
| `consentGiven` | Must be `true` |

**Business errors**
| Status | Description |
|---|---|
| `409` | Email already registered |
| `409` | Tax ID already registered |

---

#### `POST /api/v1/auth/login`
Authenticate and receive tokens. The refresh token is set as an `httpOnly` cookie (see [Refresh Token Cookie](#refresh-token-cookie)); it is not returned in the body.

- Auth: No
- Response: `200 OK` → `AuthTokenResponse`

**Request body**
```json
{
  "email": "string",
  "password": "string"
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format |
| `password` | Required |

**Business errors**
| Status | Description |
|---|---|
| `401` | Invalid credentials |
| `401` | Account not active (pending verification or deleted) |
| `401` | Account locked — after 5 failed attempts, locked for 15 minutes |

---

#### `POST /api/v1/auth/refresh`
Rotate the refresh token and get a new access token. The refresh token is read from the `refreshToken` cookie (**not** the body) and rotated — the response sets a new cookie. Requires `credentials: 'include'`.

- Auth: No
- Response: `200 OK` → `AuthTokenResponse`

**Request body**
```json
{
  "email": "string"
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format |
| `refreshToken` cookie | Required · max 512 chars — missing/invalid cookie yields the same `422` as a bad token |

**Business errors**
| Status | Description |
|---|---|
| `422` | Token invalid, expired, or cookie missing |

---

#### `POST /api/v1/auth/logout`
Invalidate the refresh token and clear the cookie. Always returns `204` (idempotent) — including when the cookie is already missing. Requires `credentials: 'include'`.

- Auth: No
- Response: `204 No Content`

**Request body**
```json
{
  "email": "string"
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format |

---

#### `POST /api/v1/auth/verify-email`
Verify a user's email using the token sent after registration.

- Auth: No
- Response: `200 OK` → `UserResponse`

**Request body**
```json
{
  "email": "string",
  "token": "string"
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format |
| `token` | Required · max 512 chars |

**Business errors**
| Status | Description |
|---|---|
| `404` | User not found |
| `400` | Token invalid or expired |

---

#### `POST /api/v1/auth/forgot-password`
Send a password reset email. Always returns `204` regardless of whether the email exists (prevents enumeration).

- Auth: No
- Response: `204 No Content`

**Request body**
```json
{
  "email": "string"
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format |

---

#### `POST /api/v1/auth/reset-password`
Set a new password using the token received by email.

- Auth: No
- Response: `204 No Content`

**Request body**
```json
{
  "email": "string",
  "token": "string",
  "newPassword": "string"
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `email` | Required · valid format |
| `token` | Required · max 512 chars |
| `newPassword` | Required · 12–128 chars · must have uppercase, lowercase, digit, and symbol |

**Business errors**
| Status | Description |
|---|---|
| `404` | User not found |
| `400` | Token invalid or expired |

---

### Users — `* /api/v1/users/me`

All user endpoints require a valid JWT in the `Authorization: Bearer {token}` header. The `userId` is extracted from the `sub` claim of the token.

#### `GET /api/v1/users/me`
Get the authenticated user's profile.

- Auth: **Yes**
- Response: `200 OK` → `UserResponse`

**Business errors**
| Status | Description |
|---|---|
| `401` | Missing or invalid token |
| `404` | User not found |

---

#### `DELETE /api/v1/users/me`
Anonymize and soft-delete the authenticated user's account (LGPD Art. 18 VI).

- Auth: **Yes**
- Response: `204 No Content`

After deletion:
- Account status becomes `Deleted`
- Email replaced with `deleted_{id}@anonymized.local`
- Tax ID replaced with `ANONYMIZED`
- Password hash replaced with `ANONYMIZED`
- All refresh tokens invalidated

**Business errors**
| Status | Description |
|---|---|
| `401` | Missing or invalid token |

---

#### `GET /api/v1/users/me/data-export`
Export all personal data held about the authenticated user (LGPD Art. 18 IV).

- Auth: **Yes**
- Response: `200 OK`

```json
{
  "id": "uuid",
  "email": "string",
  "taxId": "string",
  "role": "string",
  "status": "string",
  "createdAt": "ISO 8601",
  "consentGivenAt": "ISO 8601 | null",
  "essentialConsentRevokedAt": "ISO 8601 | null",
  "marketingConsentGranted": false,
  "marketingConsentGivenAt": "ISO 8601 | null",
  "marketingConsentRevokedAt": "ISO 8601 | null",
  "auditHistory": [
    {
      "eventType": "string",
      "occurredAt": "ISO 8601"
    }
  ]
}
```

**Business errors**
| Status | Description |
|---|---|
| `401` | Missing or invalid token |
| `404` | User not found |

---

#### `GET /api/v1/users/me/consent`
Get the authenticated user's current consent state per purpose (Essential, Marketing) — LGPD
Art. 8 §5 (consent must be revocable/inspectable at any time).

- Auth: **Yes**
- Response: `200 OK` → `ConsentResponse`

**Business errors**
| Status | Description |
|---|---|
| `401` | Missing or invalid token |
| `404` | User not found |

---

#### `PUT /api/v1/users/me/consent`
Grant or revoke consent for one purpose.

- Auth: **Yes**
- Response: `200 OK` → `ConsentResponse`

**Request body**
```json
{
  "purpose": "Essential | Marketing",
  "granted": true
}
```

**Validation rules**
| Field | Rules |
|---|---|
| `purpose` | Required · one of `Essential`, `Marketing` |
| `granted` | Required |

**Behavior**
| Purpose | `granted: false` (revoke) | `granted: true` (grant) |
|---|---|---|
| `Essential` | Suspends the account (`status` → `Suspended`); subsequent `login`/`refresh`/`reset-password`/`verify-email` calls fail until re-granted | Reactivates the account (`status` → `Active`) |
| `Marketing` | No account effect — only signals that marketing communications should stop | No account effect |

Revoking is idempotent (repeating it is a no-op success). Revoking Essential does **not** delete
or anonymize any data — that remains exclusive to `DELETE /api/v1/users/me`.

**Business errors**
| Status | Description |
|---|---|
| `401` | Missing or invalid token |
| `404` | User not found |
| `422` | `purpose` missing or not `Essential`/`Marketing` |

---

## Token Flow

```
1. POST /auth/register        → receive UserResponse (status: PendingVerification)
2. POST /auth/verify-email    → receive UserResponse (status: Active)
3. POST /auth/login           → receive accessToken in body; refreshToken set as httpOnly cookie
4. GET  /users/me             → Authorization: Bearer {accessToken}
5. POST /auth/refresh         → credentials:'include'; rotates accessToken + refreshToken cookie (every 15 min)
6. POST /auth/logout          → credentials:'include'; invalidates refreshToken and clears the cookie
```

**Frontend token storage:**
- `accessToken` — keep in memory only (JS variable / app state), never in `localStorage`/`sessionStorage`. It is lost on page reload; re-fetch it via `/auth/refresh` (step 5) on app bootstrap, since the browser still holds the refresh cookie.
- `refreshToken` — never touched by frontend code. The browser stores and sends it automatically as long as requests to `/api/v1/auth/*` are made with credentials included.

---

## Validation Constants

| Constant | Value |
|---|---|
| Email max length | 320 |
| Password min length | 12 |
| Password max length | 128 |
| Token max length | 512 |
| Max failed login attempts | 5 |
| Lockout duration | 15 minutes |
| Email verification token lifetime | 24 hours |
| Password reset token lifetime | 1 hour |
