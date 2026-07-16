namespace RentifyxIdentity.Application.Outbox;

/// <summary>
/// Field-for-field reproduction of rentifyx-communications-api's DispatchNotificationRequest wire shape
/// (docs/contracts/notification-requested.md in that repo). No shared code/package between the two
/// repos/solutions - this is the agreed JSON contract, kept in sync manually.
/// </summary>
internal sealed record NotificationRequestedMessage(
    Guid CorrelationId,
    Guid RecipientId,
    string RecipientEmail,
    string Channel,
    string TemplateId,
    IReadOnlyDictionary<string, string> Payload);
