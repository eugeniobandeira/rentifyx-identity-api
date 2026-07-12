using RentifyxIdentity.Domain.Contracts;

namespace RentifyxIdentity.Application.Features.Identity.User.ExportData;

public sealed record UserDataExportResponse(
    Guid Id,
    string Email,
    string TaxId,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConsentGivenAt,
    DateTimeOffset? EssentialConsentRevokedAt,
    bool MarketingConsentGranted,
    DateTimeOffset? MarketingConsentGivenAt,
    DateTimeOffset? MarketingConsentRevokedAt,
    IReadOnlyList<AuditLogEntryRecord> AuditHistory
);
