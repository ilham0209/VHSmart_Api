using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Bulk upload of premises (spec 7.7 + 21.10): POST only, .xls/.xlsx, maximum 250 data rows,
// partial success with a per-row error list. The column set comes from the owner's real
// templates (Q6 answered - "Factory List Bulk Upload Template.xlsx" and
// "Restaurant & Cafe List Bulk Upload Template.xlsx" in the repo root): the header row (row
// 4) is located by text and columns are matched by header name, so column order may vary.
// Store Code only exists in the Restaurant & Cafe file; a STATE column is read when the
// owner adds one (neither current file has it - the State column then stays empty).
// FOR COMPANY is mandatory in the template but the company always comes from the JWT
// (CodingRules 8.1): the cell must name the caller's own company, anything else fails the
// row. Per-row messages reuse the CreatePremise texts; the first failure per row wins so the
// error list stays one line per spreadsheet row.
public record BulkUploadPremisesCommand(IFormFile? File)
    : IRequest<BulkUploadPremisesResponse>;

public record BulkUploadPremisesResponse(
    int TotalRows,
    int UploadedRows,
    IReadOnlyList<BulkUploadRowError> Errors);

public record BulkUploadRowError(int RowNumber, string Message);

public class BulkUploadPremisesValidator : AbstractValidator<BulkUploadPremisesCommand>
{
    public BulkUploadPremisesValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("File is required.");
    }
}

public class BulkUploadPremisesHandler(
    VHSmartDbContext db,
    ICurrentUser user)
    : IRequestHandler<BulkUploadPremisesCommand, BulkUploadPremisesResponse>
{
    // D-22's 10 MB ceiling with the bulk formats (spec 21.10) instead of the document list.
    private static readonly IReadOnlyList<string> AllowedExtensions = ["xls", "xlsx"];
    private const int MaxDataRows = 250; // spec 21.10

    // ExcelDataReader resolves legacy code pages (1252) when a reader is created; without
    // this provider Encoding.GetEncoding throws. Registered exactly once per process.
    static BulkUploadPremisesHandler() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private enum Column
    {
        Ignore,
        Company,
        PremiseType,
        StoreCode,
        Name,
        Email,
        BusinessRegistrationNo,
        GoogleMapLink,
        Address1,
        Address2,
        Address3,
        Postcode,
        City,
        District,
        State,
        Country,
        Telephone,
        Fax,
        Status
    }

    public async Task<BulkUploadPremisesResponse> Handle(
        BulkUploadPremisesCommand request,
        CancellationToken ct)
    {
        if (request.File is not { } file)
            throw new BusinessRuleException("File is required.");

        FileValidation.Validate(file.FileName, file.Length, AllowedExtensions);

        var rows = await ReadRowsAsync(file); // rows[0] is the header row
        // The owner's templates carry a formatted but empty tail (rows 5-50). Blank rows are
        // ignored entirely - they neither count nor produce errors - so an untouched template
        // reports "no data rows" instead of 46 x "Company not found.".
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
            ?? throw new BusinessRuleException(
                "The file does not match the premise bulk upload template.");
        if (!HasRequiredColumns(columns))
            throw new BusinessRuleException(
                "The file does not match the premise bulk upload template.");

        var companyId = user.CompanyId;
        var companyName = await db.Companies.AsNoTracking()
            .Where(row => row.Id == companyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct);
        var countries = (await db.Countries.AsNoTracking()
                .Select(row => new { row.Id, row.Name })
                .ToListAsync(ct))
            .Select(row => (row.Id, row.Name))
            .ToList();
        var existing = await db.Premises.AsNoTracking()
            .Where(row => row.CompanyId == companyId)
            .Select(row => new { row.Email, row.StoreCode })
            .ToListAsync(ct);
        var usedEmails = existing
            .Select(row => row.Email)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedStoreCodes = existing
            .Select(row => row.StoreCode)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<BulkUploadRowError>();
        var accepted = new List<PremiseEntity>();
        foreach (var row in dataRows)
        {
            var error = ValidateRow(
                row, columns, companyName, countries, usedEmails, usedStoreCodes);
            if (error is not null)
            {
                errors.Add(new BulkUploadRowError(row.RowNumber, error));
                continue;
            }

            var entity = BuildPremise(row, columns, companyId, countries);
            usedEmails.Add(entity.Email);
            if (entity.StoreCode is not null)
                usedStoreCodes.Add(entity.StoreCode);
            accepted.Add(entity);
        }

        if (accepted.Count > 0)
        {
            db.Premises.AddRange(accepted);
            await db.SaveChangesAsync(ct);
        }

        return new BulkUploadPremisesResponse(totalRows, accepted.Count, errors);
    }

    private sealed record SheetRow(int RowNumber, string[] Cells);

    // The first worksheet (the templates name it Sheet1; spec 21.10 says "usually sheet1").
    // The header row is the first row mentioning PREMISE TYPE - rows 1-3 are legend/group
    // titles in the owner's files.
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
                row.Cells.Any(cell => CellHeader(cell).Contains("PREMISE TYPE")));
            if (headerIndex < 0)
                throw new BusinessRuleException(
                    "The file does not match the premise bulk upload template.");
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
    // markers removed, upper-cased. "PREMISE ADDRESS (LINE 1) *" keeps its line number.
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

    private static Column MatchColumn(string header) => header switch
    {
        var text when text.Contains("PREMISE TYPE") => Column.PremiseType,
        var text when text.Contains("STORE CODE") => Column.StoreCode,
        var text when text.Contains("PREMISE NAME") => Column.Name,
        var text when text.Contains("PREMISE EMAIL") => Column.Email,
        var text when text.Contains("BUSINESS REGISTRATION") => Column.BusinessRegistrationNo,
        var text when text.Contains("GOOGLE MAP") => Column.GoogleMapLink,
        var text when text.Contains("ADDRESS") && text.Contains("LINE 1") => Column.Address1,
        var text when text.Contains("ADDRESS") && text.Contains("LINE 2") => Column.Address2,
        var text when text.Contains("ADDRESS") && text.Contains("LINE 3") => Column.Address3,
        var text when text.Contains("POSTCODE") => Column.Postcode,
        var text when text.Contains("CITY") => Column.City,
        var text when text.Contains("DISTRICT") => Column.District,
        var text when text.Contains("STATE") => Column.State,
        var text when text.Contains("COUNTRY") => Column.Country,
        var text when text.Contains("TELEPHONE") => Column.Telephone,
        var text when text.Contains("FAX") => Column.Fax,
        var text when text.Contains("STATUS") => Column.Status,
        var text when text.Contains("FOR") && text.Contains("COMPANY") => Column.Company,
        _ => Column.Ignore
    };

    private static bool HasRequiredColumns(IReadOnlyDictionary<Column, int> columns) =>
        columns.ContainsKey(Column.Company)
        && columns.ContainsKey(Column.PremiseType)
        && columns.ContainsKey(Column.Name)
        && columns.ContainsKey(Column.Email)
        && columns.ContainsKey(Column.Address1)
        && columns.ContainsKey(Column.Address2)
        && columns.ContainsKey(Column.Address3)
        && columns.ContainsKey(Column.Postcode)
        && columns.ContainsKey(Column.Country)
        && columns.ContainsKey(Column.Telephone)
        && columns.ContainsKey(Column.Status);

    private static string Cell(SheetRow row, IReadOnlyDictionary<Column, int> columns, Column column) =>
        columns.TryGetValue(column, out var index)
            && index < row.Cells.Length
            ? row.Cells[index]
            : string.Empty;

    private string? ValidateRow(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        string? companyName,
        IReadOnlyList<(Guid Id, string Name)> countries,
        HashSet<string> usedEmails,
        HashSet<string> usedStoreCodes)
    {
        var company = Cell(row, columns, Column.Company);
        if (company.Length == 0
            || companyName is null
            || !string.Equals(company, companyName, StringComparison.OrdinalIgnoreCase))
            return "Company not found.";

        var type = Cell(row, columns, Column.PremiseType);
        if (type.Length == 0)
            return "Premise type is required.";
        if (!TryParsePremiseType(type, out _))
            return "Premise type is invalid.";

        var name = Cell(row, columns, Column.Name);
        if (name.Length == 0)
            return "Premise name is required.";
        if (name.Length > 200)
            return "Premise name must be 200 characters or fewer.";

        var email = Cell(row, columns, Column.Email);
        if (email.Length == 0)
            return "Premise email is required.";
        if (email.Length > 254)
            return "Premise email must be 254 characters or fewer.";
        if (usedEmails.Contains(email))
            return "A premise with this e-mail already exists.";

        var storeCode = Cell(row, columns, Column.StoreCode);
        if (storeCode.Length > 50)
            return "Store code must be 50 characters or fewer.";
        if (storeCode.Length > 0 && usedStoreCodes.Contains(storeCode))
            return "A premise with this store code already exists.";

        var businessRegistrationNo = Cell(row, columns, Column.BusinessRegistrationNo);
        if (businessRegistrationNo.Length > 50)
            return "Business registration number must be 50 characters or fewer.";
        var googleMapLink = Cell(row, columns, Column.GoogleMapLink);
        if (googleMapLink.Length > 500)
            return "Google Map link must be 500 characters or fewer.";

        foreach (var (column, required, message, lengthMessage) in new[]
                 {
                     (Column.Address1, true, "Address line 1 is required.",
                         "Address line 1 must be 200 characters or fewer."),
                     (Column.Address2, true, "Address line 2 is required.",
                         "Address line 2 must be 200 characters or fewer."),
                     (Column.Address3, true, "Address line 3 is required.",
                         "Address line 3 must be 200 characters or fewer.")
                 })
        {
            var address = Cell(row, columns, column);
            if (required && address.Length == 0)
                return message;
            if (address.Length > 200)
                return lengthMessage;
        }

        var postcode = Cell(row, columns, Column.Postcode);
        if (postcode.Length == 0)
            return "Postcode is required.";
        if (postcode.Length > 20)
            return "Postcode must be 20 characters or fewer.";
        if (Cell(row, columns, Column.City).Length > 100)
            return "City must be 100 characters or fewer.";
        if (Cell(row, columns, Column.District).Length > 100)
            return "District must be 100 characters or fewer.";
        if (Cell(row, columns, Column.State).Length > 100)
            return "State must be 100 characters or fewer.";

        var country = Cell(row, columns, Column.Country);
        if (country.Length == 0)
            return "Country is required.";
        if (!countries.Any(entry =>
                string.Equals(entry.Name, country, StringComparison.OrdinalIgnoreCase)))
            return "Country not found.";

        var telephone = Cell(row, columns, Column.Telephone);
        if (telephone.Length == 0)
            return "Telephone is required.";
        if (telephone.Length > 30)
            return "Telephone must be 30 characters or fewer.";
        if (Cell(row, columns, Column.Fax).Length > 30)
            return "Fax must be 30 characters or fewer.";

        var status = Cell(row, columns, Column.Status);
        if (status.Length == 0)
            return "Status is required.";
        if (status.Length > 30)
            return "Status must be 30 characters or fewer.";

        return null;
    }

    private PremiseEntity BuildPremise(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        Guid companyId,
        IReadOnlyList<(Guid Id, string Name)> countries)
    {
        var countryName = Cell(row, columns, Column.Country);
        var countryId = countries
            .First(entry => string.Equals(entry.Name, countryName, StringComparison.OrdinalIgnoreCase))
            .Id;

        return new PremiseEntity
        {
            CompanyId = companyId,
            PremiseType = ParsePremiseType(Cell(row, columns, Column.PremiseType)),
            Name = Cell(row, columns, Column.Name),
            Email = Cell(row, columns, Column.Email),
            StoreCode = NullIfBlank(Cell(row, columns, Column.StoreCode)),
            BusinessRegistrationNo = NullIfBlank(
                Cell(row, columns, Column.BusinessRegistrationNo)),
            GoogleMapLink = NullIfBlank(Cell(row, columns, Column.GoogleMapLink)),
            Address1 = Cell(row, columns, Column.Address1),
            Address2 = Cell(row, columns, Column.Address2),
            Address3 = NullIfBlank(Cell(row, columns, Column.Address3)),
            Postcode = Cell(row, columns, Column.Postcode),
            City = NullIfBlank(Cell(row, columns, Column.City)),
            District = NullIfBlank(Cell(row, columns, Column.District)),
            CountryId = countryId,
            // Neither owner template has a STATE column; an added one is read, otherwise the
            // cell stays empty (flagged in the report - the schema allows "" but the form
            // requires a value on the next edit).
            State = Cell(row, columns, Column.State),
            Telephone = Cell(row, columns, Column.Telephone),
            Fax = NullIfBlank(Cell(row, columns, Column.Fax)),
            Status = Cell(row, columns, Column.Status)
        };
    }

    private static string? NullIfBlank(string value) =>
        value.Length == 0 ? null : value;

    // The templates pin the dropdown per file ("Factory" / "Restaurants & Cafe"); the other
    // two spec display names (7.7) and the enum spellings are accepted too, case-insensitive.
    private static bool TryParsePremiseType(string value, out PremiseType premiseType)
    {
        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal);
        premiseType = normalized switch
        {
            "factory" => PremiseType.Factory,
            "centralkitchen" => PremiseType.CentralKitchen,
            "coldroomandwarehouse" or "coldroomwarehouse" => PremiseType.ColdRoomAndWarehouse,
            "restaurantsandcafe" or "restaurantscafe" => PremiseType.RestaurantsAndCafe,
            _ => default
        };
        return normalized is "factory" or "centralkitchen"
            or "coldroomandwarehouse" or "coldroomwarehouse"
            or "restaurantsandcafe" or "restaurantscafe";
    }

    private static PremiseType ParsePremiseType(string value)
    {
        TryParsePremiseType(value, out var premiseType);
        return premiseType;
    }
}

