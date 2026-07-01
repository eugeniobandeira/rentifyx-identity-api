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
    IReadOnlyList<AuditLogEntryRecord> AuditHistory
);
