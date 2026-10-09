namespace VHSmart_Api.Shared.Domain.HalalApplication;

// One certificate number of an approved application (spec 12.8 "List of Halal Certificate",
// Database.md 10 "AppHalalCertificates" [T]). One certificate covers many
// AppCertificateItems (a single number may certify ten products), so the items point here
// and the list counts them. UQ (CompanyId, CertificateNo): a company's number identifies
// exactly one row. Document is the uploaded certificate file - optional until the manual
// flow's step 5 uploads it (12.8). ExpiryDate null or 9999-12-31 means "no expiry" and the
// Valid / Expired column is derived from it, never stored (D-04).
public class HalalCertificateEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ApplicationId { get; set; }

    public string CertificateNo { get; set; } = string.Empty;

    public DateOnly? IssuedDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public StoredFile? Document { get; set; }
}
