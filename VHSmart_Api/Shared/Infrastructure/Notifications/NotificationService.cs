using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Shared.Infrastructure.Notifications;

public sealed class NotificationService(VHSmartDbContext db) : INotificationService
{
    private const int MaxSubjectLength = 200;
    private const int MaxMessageLength = 1000;
    private const int MaxLinkUrlLength = 300;

    public async Task PublishAsync(
        Guid userId,
        string subject,
        string message,
        string? linkUrl = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        Validate(subject, message, linkUrl);

        db.Notifications.Add(Create(userId, subject, message, linkUrl, companyId));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishManyAsync(
        IReadOnlyCollection<Guid> userIds,
        string subject,
        string message,
        string? linkUrl = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        Validate(subject, message, linkUrl);

        // No target user (nobody holds the role, nobody is linked to the company): the caller's
        // action must not fail because there is nobody to notify.
        if (userIds.Count == 0)
            return;

        foreach (var userId in userIds)
            db.Notifications.Add(Create(userId, subject, message, linkUrl, companyId));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationEntity Create(
        Guid userId, string subject, string message, string? linkUrl, Guid? companyId) =>
        new()
        {
            UserId = userId,
            CompanyId = companyId,
            Subject = subject,
            Message = message,
            LinkUrl = linkUrl,
            IsRead = false
        };

    // The message is composed by code from fixed templates, so an over-long value is a bug in
    // the caller - failing loudly beats silently losing the tail of the text.
    private static void Validate(string subject, string message, string? linkUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (subject.Length > MaxSubjectLength)
            throw new ArgumentException(
                $"Subject exceeds {MaxSubjectLength} characters.", nameof(subject));

        if (message.Length > MaxMessageLength)
            throw new ArgumentException(
                $"Message exceeds {MaxMessageLength} characters.", nameof(message));

        if (linkUrl?.Length > MaxLinkUrlLength)
            throw new ArgumentException(
                $"LinkUrl exceeds {MaxLinkUrlLength} characters.", nameof(linkUrl));
    }
}
