namespace VHSmart_Api.Shared.Infrastructure.Notifications;

// In-app notifications (D-25): the task that owns an action decides what message is sent,
// this service only stores the bell rows. Delivery by e-mail is a different, HOLD concern.
public interface INotificationService
{
    Task PublishAsync(
        Guid userId,
        string subject,
        string message,
        string? linkUrl = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default);

    // Same message to several users (one row each). No targets = nothing to send.
    Task PublishManyAsync(
        IReadOnlyCollection<Guid> userIds,
        string subject,
        string message,
        string? linkUrl = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default);
}
