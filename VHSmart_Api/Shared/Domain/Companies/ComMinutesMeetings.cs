namespace VHSmart_Api.Shared.Domain.Companies;

// Company > Internal Halal Committee (spec 7.6, Minutes Meeting list + form): one row per
// meeting. Title, MeetingDate and Location are required (Database.md 4 markers), StartTime
// and EndTime are required per the spec form and validated server-side per D-14 (the legacy
// data quirk allows Start > End - the new system rejects it). Database.md defines no unique
// index for this table, so the title is free to repeat.
public class MinutesMeetingEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime MeetingDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Location { get; set; } = string.Empty;
}
