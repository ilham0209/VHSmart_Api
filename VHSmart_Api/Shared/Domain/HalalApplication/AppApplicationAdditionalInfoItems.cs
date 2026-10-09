namespace VHSmart_Api.Shared.Domain.HalalApplication;

// "Additional Information" tab checkboxes (spec 12.5, Database.md 10): one row per ticked
// option, Section picks the tab half and OptionCode carries the option (codes follow the
// Database.md examples CARTON_BOX / HACCP / MS_ISO). FreeText holds the "Others" text, the
// MS ISO text and the cleaning explanation. Tenant [T].
public enum ApplicationAdditionalInfoSection
{
    Packaging,
    QualityControl
}

public class ApplicationAdditionalInfoItemEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid ApplicationId { get; set; }

    public ApplicationAdditionalInfoSection Section { get; set; }

    public string OptionCode { get; set; } = string.Empty;

    public string? FreeText { get; set; }
}
