namespace VHSmart_Api.Features.Admin.Companies;

// D-13: Registration Type, Owner Status, Industry Size and Market are free string columns
// (Database.md 3) whose option lists live in code with the values seen in the spec - the spec
// shows them as "e.g." examples, so the API serves the lists but the validators do not check
// the values. Not part of General Data (D-13).
internal static class CompanyOptionsCatalog
{
    // Spec 7.1: "(e.g. Companies Commission Of Malaysia)".
    public static IReadOnlyList<string> RegistrationTypes { get; } =
        ["Companies Commission Of Malaysia"];

    // Spec 7.1: "(e.g. Muslim Owner)".
    public static IReadOnlyList<string> OwnerStatuses { get; } =
        ["Muslim Owner"];

    // Spec 7.1: "(e.g. Medium Small Company)".
    public static IReadOnlyList<string> IndustrySizes { get; } =
        ["Medium Small Company"];

    // D-13 writes "Overseas / Domestic"; the slash separates the two options (spec 7.1 shows
    // "(e.g. Overseas)").
    public static IReadOnlyList<string> Markets { get; } =
        ["Overseas", "Domestic"];
}
