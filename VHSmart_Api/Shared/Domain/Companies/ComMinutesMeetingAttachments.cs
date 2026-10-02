namespace VHSmart_Api.Shared.Domain.Companies;

// Company > Internal Halal Committee (spec 7.6 meeting form, tab "Attachment": Upload
// Document + List of Attachments (#, File Name, Action)). Document is the File column group
// - Database.md 4 marks no asterisk, so a row may exist without bytes (the same reading the
// training modules got). Bytes live in IFileStorage (F-06).
public class MinutesMeetingAttachmentEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid MinutesMeetingId { get; set; }

    public MinutesMeetingEntity? MinutesMeeting { get; set; }

    public StoredFile? Document { get; set; }
}
