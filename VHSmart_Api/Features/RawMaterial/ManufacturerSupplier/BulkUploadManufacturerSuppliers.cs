using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Bulk upload of Manufacturer & Supplier rows (spec 10.1 + 21.10): POST only, .xls/.xlsx,
// maximum 250 data rows, partial success with a per-row error list. The column set comes from
// the owner's real template (Q6 answered - "ManufacturerSupplier List Bulk Upload
// Template_Standard_v9.xlsx" in the repo root): the header row (row 4) is located by text and
// columns are matched by header name, so column order may vary.
// The file carries ONE name/address/country/... set per row (plus Business Registration No.
// and Webpage), so those cells feed both halves of the command and ManufacturerSupplierData
// clears the half the Type does not keep - bulk and form can never disagree about the row
// (spec 10.1: the absent half shows N/A). CATEGORY is the caller's own "Manufacturer Type"
// general data; it is read only when the manufacturer half exists, because a supplier-only
// row stores no manufacturer half (Database.md 8) - the cell is ignored for such a row.
// E-mail uniqueness (spec 21.9) is checked per half against the caller's company and the rows
// accepted earlier in this batch; the messages reuse the Create texts. "N/A" in a cell means
// "no data" (the template says so) and is read as blank. CompanyId comes from the JWT only
// (CodingRules 8.1). The first failure per row wins so the error list stays one line per
// spreadsheet row (spec 21.10), and RowNumber is the physical sheet row.
public record BulkUploadManufacturerSuppliersCommand(IFormFile? File)
    : IRequest<BulkUploadManufacturerSuppliersResponse>;

public record BulkUploadManufacturerSuppliersResponse(
    int TotalRows,
    int UploadedRows,
    IReadOnlyList<BulkUploadManufacturerSupplierRowError> Errors);

public record BulkUploadManufacturerSupplierRowError(int RowNumber, string Message);

public class BulkUploadManufacturerSuppliersValidator
    : AbstractValidator<BulkUploadManufacturerSuppliersCommand>
{
    public BulkUploadManufacturerSuppliersValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("File is required.");
    }
}

public class BulkUploadManufacturerSuppliersHandler(
    VHSmartDbContext db,
    ICurrentUser user)
    : IRequestHandler<BulkUploadManufacturerSuppliersCommand, BulkUploadManufacturerSuppliersResponse>
{
    // D-22's 10 MB ceiling with the bulk formats (spec 21.10) instead of the document list.
    private static readonly IReadOnlyList<string> AllowedExtensions = ["xls", "xlsx"];
    private const int MaxDataRows = 250; // spec 21.10

    // The Category literal of spec 5.1 / GeneralDataCatalog (COMPANY / Manufacturer Type).
    private const string ManufacturerTypeCategory = "Manufacturer Type";

    private const string TemplateMismatch =
        "The file does not match the manufacturer and supplier bulk upload template.";

    // ExcelDataReader resolves legacy code pages (1252) when a reader is created; without
    // this provider Encoding.GetEncoding throws. Registered exactly once per process.
    static BulkUploadManufacturerSuppliersHandler() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private enum Column
    {
        Ignore,
        Type,
        Name,
        BusinessRegistrationNo,
        Category,
        Address,
        Country,
        PersonInCharge,
        ContactNumber,
        Email,
        Webpage
    }

    public async Task<BulkUploadManufacturerSuppliersResponse> Handle(
        BulkUploadManufacturerSuppliersCommand request,
        CancellationToken ct)
    {
        if (request.File is not { } file)
            throw new BusinessRuleException("File is required.");

        FileValidation.Validate(file.FileName, file.Length, AllowedExtensions);

        var rows = await ReadRowsAsync(file); // rows[0] is the header row
        // The owner's templates carry a formatted but empty tail. Blank rows are ignored
        // entirely - they neither count nor produce errors - so an untouched template reports
        // "no data rows." instead of one error per empty line.
        var dataRows = rows.Skip(1)
            .Where(row => row.Cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();
        var totalRows = dataRows.Count;
        if (totalRows == 0)
            throw new BusinessRuleException("The file contains no data rows.");
        if (totalRows > MaxDataRows)
            throw new BusinessRuleException(
                $"The file exceeds the limit of {MaxDataRows} rows.");

        var columns = MapColumns(rows[0].Cells)
            ?? throw new BusinessRuleException(TemplateMismatch);
        if (!HasRequiredColumns(columns))
            throw new BusinessRuleException(TemplateMismatch);

        var companyId = user.CompanyId;
        var countries = (await db.Countries.AsNoTracking()
                .Select(row => new { row.Id, row.Name })
                .ToListAsync(ct))
            .Select(row => (row.Id, row.Name))
            .ToList();
        var manufacturerTypes = (await db.GeneralData.AsNoTracking()
                .Where(row => row.CompanyId == companyId
                    && row.Category == ManufacturerTypeCategory)
                .Select(row => new { row.Id, row.Name })
                .ToListAsync(ct))
            .Select(row => (row.Id, row.Name))
            .ToList();
        var existing = await db.ManufacturerSuppliers.AsNoTracking()
            .Where(row => row.CompanyId == companyId)
            .Select(row => new { row.ManufacturerEmail, row.SupplierEmail })
            .ToListAsync(ct);
        var usedManufacturerEmails = existing
            .Select(row => row.ManufacturerEmail)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedSupplierEmails = existing
            .Select(row => row.SupplierEmail)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<BulkUploadManufacturerSupplierRowError>();
        var accepted = new List<ManufacturerSupplierEntity>();
        foreach (var row in dataRows)
        {
            var error = ValidateRow(
                row, columns, countries, manufacturerTypes,
                usedManufacturerEmails, usedSupplierEmails);
            if (error is not null)
            {
                errors.Add(new BulkUploadManufacturerSupplierRowError(row.RowNumber, error));
                continue;
            }

            var entity = BuildRow(row, columns, companyId, countries, manufacturerTypes);
            if (entity.ManufacturerEmail is not null)
                usedManufacturerEmails.Add(entity.ManufacturerEmail);
            if (entity.SupplierEmail is not null)
                usedSupplierEmails.Add(entity.SupplierEmail);
            accepted.Add(entity);
        }

        if (accepted.Count > 0)
        {
            db.ManufacturerSuppliers.AddRange(accepted);
            await db.SaveChangesAsync(ct);
        }

        return new BulkUploadManufacturerSuppliersResponse(totalRows, accepted.Count, errors);
    }

    private sealed record SheetRow(int RowNumber, string[] Cells);

    // The first worksheet (the templates name it Sheet1; spec 21.10 says "usually sheet1").
    // The header row carries TYPE - rows 1-3 are the legend and the group title in the owner's
    // file ("MANUFACTURER / SUPPLIER NAME INFORMATION"), which has no TYPE cell.
    private static async Task<List<SheetRow>> ReadRowsAsync(IFormFile file)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var all = new List<SheetRow>();
            var rowNumber = 0; // physical sheet row (ExcelDataReader yields blank rows too)
            while (reader.Read())
            {
                rowNumber++;
                var cells = new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                    cells[i] = CellText(reader, i);
                all.Add(new SheetRow(rowNumber, cells));
            }

            var headerIndex = all.FindIndex(row =>
                row.Cells.Any(cell => CellHeader(cell).Contains("TYPE"))
                && row.Cells.Any(cell => CellHeader(cell).Contains("MANUFACTURER")));
            if (headerIndex < 0)
                throw new BusinessRuleException(TemplateMismatch);
            return all.Skip(headerIndex).ToList();
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception)
        {
            // ExcelDataReader rejects non-workbook bytes; the user gets the file-level rule.
            throw new BusinessRuleException(
                "The file could not be read as an Excel workbook.");
        }
    }

    private static string CellText(IDataReader reader, int index) =>
        reader.GetValue(index) switch
        {
            null => string.Empty,
            string text => text.Trim(),
            double number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool flag => flag ? "TRUE" : "FALSE",
            var value => Convert.ToString(value, CultureInfo.CurrentCulture)?.Trim()
                ?? string.Empty
        };

    // Header text as the templates write it: newlines/underscores from Excel and the "(*)"
    // markers removed, upper-cased.
    private static string CellHeader(string cell) =>
        string.Join(' ', cell
            .Replace("_x000a_", " ", StringComparison.OrdinalIgnoreCase)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

    private static Dictionary<Column, int> MapColumns(string[] headerCells)
    {
        var columns = new Dictionary<Column, int>();
        for (var i = 0; i < headerCells.Length; i++)
        {
            var column = MatchColumn(CellHeader(headerCells[i]));
            if (column != Column.Ignore && !columns.ContainsKey(column))
                columns[column] = i;
        }

        return columns;
    }

    // The file has one NAME column ("MANUFACTURER / SUPPLIER NAME"), so the two headers that
    // could claim Type ("TYPE") and Name never collide.
    private static Column MatchColumn(string header) => header switch
    {
        var text when text.Contains("MANUFACTURER") && text.Contains("NAME") => Column.Name,
        var text when text.Contains("TYPE") => Column.Type,
        var text when text.Contains("BUSINESS REGISTRATION") => Column.BusinessRegistrationNo,
        var text when text.Contains("CATEGORY") => Column.Category,
        var text when text.Contains("ADDRESS") => Column.Address,
        var text when text.Contains("COUNTRY") => Column.Country,
        var text when text.Contains("PERSON IN CHARGE") => Column.PersonInCharge,
        var text when text.Contains("CONTACT") => Column.ContactNumber,
        var text when text.Contains("EMAIL") => Column.Email,
        var text when text.Contains("WEBPAGE") => Column.Webpage,
        _ => Column.Ignore
    };

    // The five columns the template marks mandatory (*): Type, Name, Category, Address,
    // Country. The other cells are optional (spec 10.1 form has optional e-mail and phone).
    private static bool HasRequiredColumns(IReadOnlyDictionary<Column, int> columns) =>
        columns.ContainsKey(Column.Type)
        && columns.ContainsKey(Column.Name)
        && columns.ContainsKey(Column.Category)
        && columns.ContainsKey(Column.Address)
        && columns.ContainsKey(Column.Country);

    // "N/A" is the template's "no data" placeholder (Q6), so it reads as blank; the rest is
    // the stored value as typed.
    private static string Cell(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        Column column)
    {
        if (!columns.TryGetValue(column, out var index) || index >= row.Cells.Length)
            return string.Empty;
        var value = row.Cells[index];
        return string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value;
    }

    private string? ValidateRow(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        IReadOnlyList<(Guid Id, string Name)> countries,
        IReadOnlyList<(Guid Id, string Name)> manufacturerTypes,
        HashSet<string> usedManufacturerEmails,
        HashSet<string> usedSupplierEmails)
    {
        var type = Cell(row, columns, Column.Type);
        if (type.Length == 0)
            return "Type is required.";
        if (!TryParseType(type, out var parsedType))
            return "Type is invalid.";

        // The file has one name/address/country set: the half that survives decides which
        // Create message applies (a supplier-only row is checked as a supplier, and so on).
        var prefix = ManufacturerSupplierData.KeepsManufacturer(parsedType)
            ? "Manufacturer"
            : "Supplier";

        var name = Cell(row, columns, Column.Name);
        if (name.Length == 0)
            return $"{prefix} name is required.";
        if (name.Length > 200)
            return $"{prefix} name must be 200 characters or fewer.";

        var address = Cell(row, columns, Column.Address);
        if (address.Length == 0)
            return $"{prefix} address is required.";
        if (address.Length > 500)
            return $"{prefix} address must be 500 characters or fewer.";

        var country = Cell(row, columns, Column.Country);
        if (country.Length == 0)
            return "Country is required.";
        if (!countries.Any(entry => SameName(entry.Name, country)))
            return "Country not found.";

        // The file marks CATEGORY mandatory, but it only feeds the manufacturer half: a
        // supplier-only row stores no Category (Database.md 8) and the cell is skipped.
        if (ManufacturerSupplierData.KeepsManufacturer(parsedType))
        {
            var category = Cell(row, columns, Column.Category);
            if (category.Length == 0)
                return "Manufacturer type is required.";
            if (!manufacturerTypes.Any(entry => SameName(entry.Name, category)))
                return "Manufacturer type not found.";
        }

        if (Cell(row, columns, Column.BusinessRegistrationNo).Length > 50)
            return "Business registration number must be 50 characters or fewer.";
        if (Cell(row, columns, Column.PersonInCharge).Length > 200)
            return "Person in charge must be 200 characters or fewer.";
        if (Cell(row, columns, Column.ContactNumber).Length > 30)
            return "Contact number must be 30 characters or fewer.";

        var email = Cell(row, columns, Column.Email);
        if (email.Length > 254)
            return "E-mail must be 254 characters or fewer.";
        if (email.Length > 0)
        {
            if (ManufacturerSupplierData.KeepsManufacturer(parsedType)
                && usedManufacturerEmails.Contains(email))
                return "A manufacturer with this e-mail already exists.";
            if (ManufacturerSupplierData.KeepsSupplier(parsedType)
                && usedSupplierEmails.Contains(email))
                return "A supplier with this e-mail already exists.";
        }

        if (Cell(row, columns, Column.Webpage).Length > 200)
            return "Webpage must be 200 characters or fewer.";

        return null;
    }

    private static ManufacturerSupplierEntity BuildRow(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        Guid companyId,
        IReadOnlyList<(Guid Id, string Name)> countries,
        IReadOnlyList<(Guid Id, string Name)> manufacturerTypes)
    {
        TryParseType(Cell(row, columns, Column.Type), out var type);
        var countryName = Cell(row, columns, Column.Country);
        var countryId = countries
            .First(entry => SameName(entry.Name, countryName))
            .Id;
        var keepsManufacturer = ManufacturerSupplierData.KeepsManufacturer(type);
        var category = Cell(row, columns, Column.Category);
        var typeId = keepsManufacturer
            ? manufacturerTypes.First(entry => SameName(entry.Name, category)).Id
            : (Guid?)null;

        // One set of cells feeds both halves; Type decides which half survives the Apply
        // (spec 10.1), so the row built here is identical to the form's row.
        var name = Cell(row, columns, Column.Name);
        var address = Cell(row, columns, Column.Address);
        var personInCharge = Cell(row, columns, Column.PersonInCharge);
        var contactNumber = Cell(row, columns, Column.ContactNumber);
        var email = NullIfBlank(Cell(row, columns, Column.Email));

        return ManufacturerSupplierData.Apply(
            new ManufacturerSupplierEntity
            {
                CompanyId = companyId
            },
            new CreateManufacturerSupplierCommand(
                type,
                name,
                NullIfBlank(Cell(row, columns, Column.BusinessRegistrationNo)),
                typeId,
                address,
                countryId,
                personInCharge,
                contactNumber,
                email,
                NullIfBlank(Cell(row, columns, Column.Webpage)),
                name,
                address,
                countryId,
                personInCharge,
                contactNumber,
                email));
    }

    // The template's dropdown: Manufacturer / Supplier / Manufacturer & Supplier.
    private static bool TryParseType(string value, out ManufacturerSupplierType type)
    {
        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal);
        switch (normalized)
        {
            case "manufacturer":
                type = ManufacturerSupplierType.ManufacturerOnly;
                return true;
            case "supplier":
                type = ManufacturerSupplierType.SupplierOnly;
                return true;
            case "manufacturersupplier":
            case "manufacturerandsupplier":
                type = ManufacturerSupplierType.Both;
                return true;
            default:
                type = default;
                return false;
        }
    }

    private static bool SameName(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? NullIfBlank(string value) =>
        value.Length == 0 ? null : value;
}
