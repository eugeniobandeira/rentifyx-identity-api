# LGPD Endpoints — Task Breakdown

## Status Legend

| Symbol | Meaning |
|---|---|
| ⬜ | Pending |
| ✅ | Complete |

## Tasks

| # | Layer | What | Status |
|---|---|---|---|
| T-01 | Domain | `UserAccountDeleted.cs` domain event | ✅ |
| T-02 | Domain | Add `AlreadyDeleted` to `UserErrorCodes.cs` | ✅ |
| T-03 | Application | `GetProfileRequest.cs` | ✅ |
| T-04 | Application | `GetProfileValidator.cs` | ✅ |
| T-05 | Application | `GetProfileHandler.cs` | ✅ |
| T-06 | Application | `DeleteAccountRequest.cs` | ✅ |
| T-07 | Application | `DeleteAccountValidator.cs` | ✅ |
| T-08 | Application | `DeleteAccountHandler.cs` | ✅ |
| T-09 | Application | `ExportDataRequest.cs` | ✅ |
| T-10 | Application | `UserDataExportResponse.cs` | ✅ |
| T-11 | Application | `ExportDataValidator.cs` | ✅ |
| T-12 | Application | `ExportDataHandler.cs` | ✅ |
| T-13 | IoC + API | Add `AddAuthentication()` + `AddAuthorization()` to IoC; `UseAuthentication()` to Program.cs | ✅ |
| T-14 | API | `GetProfile.cs` endpoint | ✅ |
| T-15 | API | `DeleteAccount.cs` endpoint | ✅ |
| T-16 | API | `ExportData.cs` endpoint | ✅ |
| T-17 | Tests | `TestAuthHandler.cs` + update `CustomWebApplicationFactory` | ✅ |
| T-18 | Tests | `GetProfileValidatorTests.cs` | ✅ |
| T-19 | Tests | `DeleteAccountValidatorTests.cs` | ✅ |
| T-20 | Tests | `ExportDataValidatorTests.cs` | ✅ |
| T-21 | Tests | `GetProfileHandlerTests.cs` | ✅ |
| T-22 | Tests | `DeleteAccountHandlerTests.cs` | ✅ |
| T-23 | Tests | `ExportDataHandlerTests.cs` | ✅ |
| T-24 | Tests | `LgpdEndpointTests.cs` | ✅ |

## Dependencies

```
T-01 → T-08
T-02 → T-08, T-22
T-03 → T-04 → T-05
T-06 → T-07 → T-08
T-09 → T-11 → T-12
T-10 → T-12
T-13 → T-14, T-15, T-16
T-14, T-15, T-16 → T-24
T-17 → T-24
T-05 → T-21; T-08 → T-22; T-12 → T-23
T-18, T-19, T-20, T-21, T-22, T-23 → T-24
```
