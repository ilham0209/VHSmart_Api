using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Excel fixtures for the premise bulk upload (spec 7.7 + 21.10): a minimal .xlsx written by
// hand (ZipArchive + inline strings, so the tests need no Excel library either), laid out
// like the owner's templates - legend rows 1-3, the header row 4, data from row 5 - using the
// exact header cells of "Factory List Bulk Upload Template.xlsx" and "Restaurant & Cafe List
// Bulk Upload Template.xlsx" (Q6), so RowNumber assertions are physical sheet rows.
internal static class BulkUploadTestData
{
    public const string CompanyName = "Verify Halal Sdn Bhd";

    // Row 4 of the Factory template (17 cells: no Store Code, no State).
    public static readonly string[] FactoryHeader =
    [
        "#",
        "FOR  COMPANY (*)",
        "PREMISE TYPE (*)",
        "PREMISE NAME (*) ",
        "PREMISE EMAIL (*)",
        "PREMISE BUSINESS REGISTRATION NO\n(Fill in if different from HQ)",
        "GOOGLE MAP LINK",
        "PREMISE ADDRESS \n(LINE 1) *",
        "PREMISE ADDRESS \n(LINE 2) *",
        "PREMISE ADDRESS \n(LINE 3) *",
        "POSTCODE (*)",
        "CITY",
        "DISTRICT",
        "COUNTRY (*)",
        "TELEPHONE NO. (*)",
        "FAX NO.",
        "STATUS (*)"
    ];

    // The Restaurant & Cafe template is the same file with STORE CODE after Premise Type.
    public static readonly string[] RestaurantHeader = BuildRestaurantHeader();

    // Rows 1-3 of the owner's files. Row 2 carries one blank cell so the sheet keeps its
    // physical numbering (a cellless row would not be written reliably).
    public static readonly string[][] Preamble =
    [
        ["(*) Mandatory"],
        [" "],
        ["PREMISE INFORMATION"]
    ];

    public static IFormFile FormFile(byte[] bytes, string fileName = "premises.xlsx") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);

    public static byte[] FactoryFile(params string[][] dataRows) =>
        Sheet([.. Preamble, FactoryHeader, .. dataRows]);

    public static byte[] RestaurantFile(params string[][] dataRows) =>
        Sheet([.. Preamble, RestaurantHeader, .. dataRows]);

    // A Factory-template data row: one cell per FactoryHeader column, in template order.
    public static string[] FactoryRow(
        string email,
        string company = CompanyName,
        string type = "Factory",
        string name = "Seri Rasa Factory",
        string brn = "",
        string map = "",
        string address1 = "1 Jalan Verify",
        string address2 = "Taman Industri",
        string address3 = "Seksyen 7",
        string postcode = "40000",
        string city = "Shah Alam",
        string district = "Selangor",
        string country = "Malaysia",
        string telephone = "0312345678",
        string fax = "",
        string status = "Active") =>
        [
            "", company, type, name, email, brn, map, address1, address2, address3,
            postcode, city, district, country, telephone, fax, status
        ];

    // A Restaurant & Cafe-template data row (Store Code in column 4).
    public static string[] RestaurantRow(
        string email,
        string storeCode = "SC-01",
        string company = CompanyName,
        string type = "Restaurants & Cafe",
        string name = "Kedai Kopi",
        string brn = "",
        string map = "",
        string address1 = "1 Jalan Verify",
        string address2 = "Taman Industri",
        string address3 = "Seksyen 7",
        string postcode = "40000",
        string city = "Shah Alam",
        string district = "Selangor",
        string country = "Malaysia",
        string telephone = "0312345678",
        string fax = "",
        string status = "Active") =>
        [
            "", company, type, storeCode, name, email, brn, map, address1, address2, address3,
            postcode, city, district, country, telephone, fax, status
        ];

    // Any sheet the test wants to assemble itself (missing columns, foreign headers, ...).
    public static byte[] Sheet(params string[][] rows)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(zip, "[Content_Types].xml", ContentTypeXml);
            Entry(zip, "_rels/.rels", RootRelsXml);
            Entry(zip, "xl/workbook.xml", WorkbookXml);
            Entry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            Entry(zip, "xl/worksheets/sheet1.xml", SheetXml(rows));
        }

        return stream.ToArray();
    }

    private static string[] BuildRestaurantHeader()
    {
        var header = new string[FactoryHeader.Length + 1];
        Array.Copy(FactoryHeader, 0, header, 0, 3);
        header[3] = "STORE CODE (*)";
        Array.Copy(FactoryHeader, 3, header, 4, FactoryHeader.Length - 3);
        return header;
    }

    private static void Entry(ZipArchive zip, string name, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(xml);
    }

    private static string SheetXml(IReadOnlyList<string[]> rows)
    {
        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        xml.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (var r = 0; r < rows.Count; r++)
        {
            var number = r + 1;
            xml.Append($"<row r=\"{number}\">");
            for (var c = 0; c < rows[r].Length; c++)
            {
                var value = rows[r][c];
                if (value.Length == 0)
                    continue;
                xml.Append($"<c r=\"{ColumnName(c)}{number}\" t=\"inlineStr\">")
                    .Append("<is><t>")
                    .Append(SecurityElement.Escape(value))
                    .Append("</t></is></c>");
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    // A, B, ... Z, AA, AB (the templates stop at column R, but stay general).
    private static string ColumnName(int index) =>
        index < 26
            ? ((char)('A' + index)).ToString()
            : $"{(char)('A' + index / 26 - 1)}{(char)('A' + index % 26)}";

    private const string ContentTypeXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
        <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
        <Default Extension="xml" ContentType="application/xml"/>
        <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
        <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private const string RootRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
        <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string WorkbookXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
        <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
        <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;
}
