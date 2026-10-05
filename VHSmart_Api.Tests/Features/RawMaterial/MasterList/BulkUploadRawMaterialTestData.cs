namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

// Excel fixtures for the raw material bulk upload (spec 10.2 + 21.10), laid out like the
// owner's real file (Q6: "Raw Material Bulk Upload Template_Standard_v10.xlsx" - legend row 1,
// blank row 2, group title row 3, header row 4, sample row 5): 23 columns (A-W) with the
// SUPPLIER block (E-K) and the MANUFACTURER block (L-R) carrying their own COUNTRY / BRN /
// PERSON IN CHARGE / CONTACT NUMBER / EMAIL.
internal static class BulkUploadRawMaterialTestData
{
    // Row 4 of the owner's template, verbatim (newlines included).
    public static readonly string[] Header =
    [
        "#",
        "RAW MATERIAL CATEGORY\n(Select from dropdown list only)",
        "INGREDIENT/RAW MATERIAL NAME (*)",
        "INGREDIENT STATUS (*)\n(Select from dropdown list only)",
        "SUPPLIER'S NAME (*)",
        "SUPPLIER'S ADDRESS (*)",
        "COUNTRY (*)\n(Select from dropdown list only)",
        "BUSINESS REGISTRATION NO.",
        "PERSON IN CHARGE",
        "CONTACT NUMBER",
        "EMAIL",
        "MANUFACTURER'S NAME (*)",
        "MANUFACTURER'S ADDRESS (*)",
        "COUNTRY (*)\n(Select from dropdown list only)",
        "BUSINESS REGISTRATION NO.",
        "PERSON IN CHARGE",
        "CONTACT NUMBER",
        "EMAIL",
        "HALAL CERTIFIED YES/NO (*)",
        "INGREDIENT/RAW MATERIAL CODE",
        "COMMERCIAL NAME",
        "SCIENTIFIC NAME",
        "INGREDIENT/RAW MATERIAL SOURCE (*)\n(Select from dropdown list only)"
    ];

    public static readonly string[][] Preamble =
    [
        ["(*) Mandatory"],
        [" "],
        ["INGREDIENTS/RAW MATERIALS INFORMATION"]
    ];

    public static byte[] File(params string[][] dataRows) =>
        BulkUploadWorkbook.Sheet([.. Preamble, Header, .. dataRows]);

    // A data row: one cell per Header column, in template order. The defaults are the
    // template's sample row, minus the two e-mails - a row with no e-mail builds a NEW
    // manufacturer & supplier row instead of matching an existing one (Q16).
    public static string[] Row(
        string category = "Core",
        string ingredient = "Badam Biji",
        string status = "Active",
        string supplierName = "VH Distributor",
        string supplierAddress = "Level 7, Menara Binjai",
        string supplierCountry = "Malaysia",
        string supplierBrn = "MA12345XXXXXX",
        string supplierPerson = "Ahmad",
        string supplierContact = "0123456789",
        string supplierEmail = "",
        string manufacturerName = "MMK Trading",
        string manufacturerAddress = "No 2 Perindustrian Mara",
        string manufacturerCountry = "Malaysia",
        string manufacturerBrn = "MA12345XXXXXX",
        string manufacturerPerson = "Abu",
        string manufacturerContact = "0123456789",
        string manufacturerEmail = "",
        string halal = "Yes",
        string code = "BA-001",
        string commercial = "SweetSpike",
        string scientific = "Prunus dulcis",
        string source = "Plant Based") =>
        [
            "sample", category, ingredient, status,
            supplierName, supplierAddress, supplierCountry, supplierBrn, supplierPerson,
            supplierContact, supplierEmail,
            manufacturerName, manufacturerAddress, manufacturerCountry, manufacturerBrn,
            manufacturerPerson, manufacturerContact, manufacturerEmail,
            halal, code, commercial, scientific, source
        ];
}
