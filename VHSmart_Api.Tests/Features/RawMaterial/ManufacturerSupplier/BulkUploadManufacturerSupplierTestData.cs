namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

// Excel fixtures for the Manufacturer & Supplier bulk upload (spec 10.1 + 21.10), laid out
// like the owner's real file (Q6: "ManufacturerSupplier List Bulk Upload Template_Standard_v9"
// - legend row 1, blank row 2, group title row 3, header row 4, data from row 5).
internal static class BulkUploadManufacturerSupplierTestData
{
    // Row 4 of the owner's template (11 cells, columns A-K).
    public static readonly string[] Header =
    [
        "#",
        "TYPE (*)",
        "MANUFACTURER / SUPPLIER NAME (*)",
        "BUSINESS REGISTRATION NO.",
        "CATEGORY (*)",
        "ADDRESS (*)",
        "COUNTRY (*)",
        "PERSON IN CHARGE",
        "CONTACT NUMBER",
        "EMAIL",
        "WEBPAGE"
    ];

    public static readonly string[][] Preamble =
    [
        ["(*) Mandatory"],
        [" "],
        ["MANUFACTURER / SUPPLIER NAME INFORMATION"]
    ];

    public static byte[] File(params string[][] dataRows) =>
        BulkUploadWorkbook.Sheet([.. Preamble, Header, .. dataRows]);

    // A data row: one cell per Header column, in template order. The file carries ONE
    // name/address/country set, so the same defaults feed whichever half Type keeps.
    public static string[] Row(
        string type = "Manufacturer & Supplier",
        string name = "Santan Foods Sdn Bhd",
        string brn = "202301001234",
        string category = "Food and Beverages",
        string address = "Jalan Gombak 1",
        string country = "Malaysia",
        string personInCharge = "Aminah",
        string contactNumber = "0380000000",
        string email = "hello@santan.example.com",
        string webpage = "https://santan.example.com") =>
        [
            "", type, name, brn, category, address, country, personInCharge, contactNumber,
            email, webpage
        ];
}
