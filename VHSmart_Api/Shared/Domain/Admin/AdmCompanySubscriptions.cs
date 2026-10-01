namespace VHSmart_Api.Shared.Domain.Admin;

// One row per subscription period (Database.md 5): the first contract is "New", every yearly
// extension adds a "Renewal" row, and the Account Setting > Subscription screen reads them
// (spec 6.4). EntryType is stored as its spec string via HasConversion<string>()
// (CodingRules 11), e.g. "Renewal".
public enum SubscriptionEntryType
{
    New,
    Renewal
}

// [T] tenant table: the history belongs to one company. ExpiryWarningSentAt is the
// idempotency stamp of the D-11 7-day warning job - a row warns once, never twice.
public class CompanySubscriptionEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid PackageId { get; set; }

    public SubscriptionEntryType EntryType { get; set; }

    // Display label of the history row, e.g. "RENEWAL : VH SMART - Advanced (1 Year)" (6.4).
    public string? Label { get; set; }

    public int DurationMonths { get; set; }

    public DateTime StartDate { get; set; }

    // Date column; 9999-12-31 = "Ongoing" (spec 13, Database.md 5).
    public DateTime EndDate { get; set; }

    public DateTime? ExpiryWarningSentAt { get; set; }
}
