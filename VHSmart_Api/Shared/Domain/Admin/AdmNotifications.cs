namespace VHSmart_Api.Shared.Domain.Admin;

// In-app bell message for one user (D-25); the triggers are listed in BusinessLogic 21.8.
// Not a tenant table: a notification belongs to a user, and the row carries the company it was
// raised for so a later "Switch Company" can still show it (Database.md, no [T] marker).
public class NotificationEntity : BaseClass
{
    public Guid UserId { get; set; }

    public Guid? CompanyId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public string? LinkUrl { get; set; }
}
