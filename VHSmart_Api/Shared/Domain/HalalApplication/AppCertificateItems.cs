namespace VHSmart_Api.Shared.Domain.HalalApplication;

// One approved product or premise of an application (spec 12.8 "List of Certificate Item",
// Database.md 10 "AppCertificateItems" [T]). Rows are created when the application becomes
// approved - the tagging handler snapshots each ACTIVE batch product / batch premise here,
// so the certificate later covers the names as they were at approval. HalalCertificateId is
// null until the "CLICK TO ADD" flow of the item list fills in a certificate number, which
// links the row to (or creates) its AppHalalCertificates row.
public class CertificateItemEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ApplicationId { get; set; }

    // Snapshot of the approved product / premise name - never a join at read time.
    public string ItemName { get; set; } = string.Empty;

    public Guid? ProductId { get; set; }

    public Guid? PremiseId { get; set; }

    public Guid? BrandId { get; set; }

    // Null = "CLICK TO ADD" in the list (spec 12.8).
    public Guid? HalalCertificateId { get; set; }
}
