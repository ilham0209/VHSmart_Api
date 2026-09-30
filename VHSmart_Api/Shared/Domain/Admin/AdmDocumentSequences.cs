namespace VHSmart_Api.Shared.Domain.Admin;

// Running numbers for reference numbers (Database.md: incremented in a transaction).
// One row per Scope - the caller's part of the key (prefix + date, + scheme for applications,
// D-09) - so two different prefixes or days never contend for the same row.
public class DocumentSequenceEntity : BaseClass
{
    public string Scope { get; set; } = string.Empty;

    // Concurrency token: with several app instances on one database the read-increment-write
    // below would otherwise lose updates (D-09 needs one number per caller, never a repeat).
    public int LastNumber { get; set; }
}
