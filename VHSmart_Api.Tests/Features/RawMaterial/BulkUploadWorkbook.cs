using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace VHSmart_Api.Tests.Features.RawMaterial;

// Excel fixtures for the RM-04 bulk uploads (spec 10.1, 10.2 + 21.10): a minimal .xlsx
// written by hand (ZipArchive + inline strings, so the tests need no Excel library either),
// laid out like the owner's templates - legend rows 1-3, the header row 4, data from row 5 -
// using the exact header cells of "Raw Material Bulk Upload Template_Standard_v10.xlsx" and
// "ManufacturerSupplier List Bulk Upload Template_Standard_v9.xlsx" (Q6), so RowNumber
// assertions are physical sheet rows. Same writer as the premise fixtures (BulkUploadTestData).
internal static class BulkUploadWorkbook
{
    public static IFormFile FormFile(byte[] bytes, string fileName = "upload.xlsx") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);

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

    // A, B, ... Z, AA, AB (the raw material template stops at column W).
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
