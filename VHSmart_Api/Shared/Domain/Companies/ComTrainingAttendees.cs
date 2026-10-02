namespace VHSmart_Api.Shared.Domain.Companies;

// Personnel > Internal Training (spec 7.5 "Attendance*"): one row per attendee per training.
// UQ (TrainingId, StaffId) among live rows (Database.md 6) - removing someone from the
// attendance list is a soft delete, so a later re-pick must not trip the index. The duplicate
// check itself is client-side only (spec 7.5 [CODE]); this index is the data-level guard.
public class TrainingAttendeeEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid TrainingId { get; set; }

    public TrainingEntity? Training { get; set; }

    public Guid StaffId { get; set; }

    public StaffEntity? Staff { get; set; }
}
