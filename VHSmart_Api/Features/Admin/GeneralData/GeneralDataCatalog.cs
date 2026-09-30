using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Features.Admin.GeneralData;

// The Group -> Category catalogue of spec 5.1 [CONFIRMED]: which categories may be chosen for
// each Group. Lives in code (Database.md 3: "allowed pairs live in code as
// GeneralDataCatalog") - the rows a company actually creates live in AdmGeneralData and are
// NOT seeded (Database.md 14, spec 3.3).
public static class GeneralDataCatalog
{
    // Pairs in the order of the spec table; groups follow the order of the spec's group list.
    private static readonly IReadOnlyList<(GeneralDataGroup Group, string Category)> Pairs =
    [
        (GeneralDataGroup.COMPANY, "Brand"),
        (GeneralDataGroup.COMPANY, "Ownership Type"),
        (GeneralDataGroup.COMPANY, "Store Concept"),
        (GeneralDataGroup.COMPANY, "Manufacturer Type"),
        (GeneralDataGroup.COMPANY, "State Grouping"),
        (GeneralDataGroup.COMPANY, "Menu Category"),
        (GeneralDataGroup.COMPANY, "Residency Type"),
        (GeneralDataGroup.COMPANY, "Prayer Room Availability"),
        (GeneralDataGroup.COMPANY, "Premise Tag"),
        (GeneralDataGroup.PEOPLE, "Title of Honour"),
        (GeneralDataGroup.PEOPLE, "Designation"),
        (GeneralDataGroup.PEOPLE, "Department"),
        (GeneralDataGroup.PEOPLE, "Internal Halal Committee Role"),
        (GeneralDataGroup.PRODUCT, "Ingredient Source"),
        (GeneralDataGroup.PRODUCT, "Ingredient Status"),
        (GeneralDataGroup.PRODUCT, "Product Category"),
        (GeneralDataGroup.PRODUCT, "Marketing Method"),
        (GeneralDataGroup.CERTIFICATES, "Certificate Status"),
        (GeneralDataGroup.PAYMENT, "Payment Category"),
        (GeneralDataGroup.AUDIT, "Audit Type"),
        (GeneralDataGroup.AUDIT, "Audit Purpose"),
        (GeneralDataGroup.AUDIT, "Non Compliance Category"),
        (GeneralDataGroup.AUDIT, "External - Audit Reference"),
        (GeneralDataGroup.AUDIT, "External - CB Non Conformance Details"),
        (GeneralDataGroup.AUDIT, "External - Audit Category"),
        (GeneralDataGroup.AUDIT, "Internal - Audit Category"),
        (GeneralDataGroup.TRAINING, "Module Type")
    ];

    public static IReadOnlyList<GeneralDataGroup> Groups { get; } =
        [.. Pairs.Select(pair => pair.Group).Distinct()];

    public static IReadOnlyList<string> Categories(GeneralDataGroup group) =>
        [.. Pairs.Where(pair => pair.Group == group).Select(pair => pair.Category)];

    // The client picks from this catalogue, but a hand-crafted request must not slip through on
    // a case difference alone.
    public static bool BelongsTo(GeneralDataGroup group, string category) =>
        Pairs.Any(pair => pair.Group == group
            && string.Equals(pair.Category, category, StringComparison.OrdinalIgnoreCase));
}
