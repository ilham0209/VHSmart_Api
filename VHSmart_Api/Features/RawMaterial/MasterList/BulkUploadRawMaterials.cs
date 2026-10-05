using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Bulk upload of raw materials (spec 10.2 + 21.10): POST only, .xls/.xlsx, maximum 250 data
// rows, partial success with a per-row error list. The column set comes from the owner's real
// template (Q6 answered - "Raw Material Bulk Upload Template_Standard_v10.xlsx" in the repo
// root): the header row (row 4) is located by text and columns are matched by header name.
// The file repeats COUNTRY / BUSINESS REGISTRATION NO. / PERSON IN CHARGE / CONTACT NUMBER /
// EMAIL once per block, so MapColumns remembers which block it is in (SUPPLIER'S NAME starts
// the supplier block, MANUFACTURER'S NAME the manufacturer one) instead of keeping the first
// hit - both halves are read.
// The two blocks become the RawMaterials.ManufacturerSupplierId row (Q16, owner answered):
// each half's e-mail is matched against the caller's own live rows and the first hit is
// linked as-is; when nothing matches (and when there are no e-mails at all) a new
// RawManufacturerSuppliers row is built from the blocks - Type from which halves are filled -
// and linked. A reused row is never updated, so only a NEW row is checked against the Create
// rules (address and country per half). "N/A" means "no data" (the template says so) and is
// read as blank; a block is present when its NAME cell carries a value, and a row with no
// block at all fails with the Create text "Manufacturer is required."
// The file has no "Accessible For" and no packaging column (Q6): Database.md 8 keeps >= 1
// accessible row, so the caller's own company is shared, and the material is created as a
// non-packaging material. HALAL CERTIFIED YES/NO has no column in Database.md and is read
// but not stored. Per-row messages reuse the CreateRawMaterial texts; the first failure per
// row wins and RowNumber is the physical sheet row (spec 21.10).
public record BulkUploadRawMaterialsCommand(IFormFile? File)
    : IRequest<BulkUploadRawMaterialsResponse>;

public record BulkUploadRawMaterialsResponse(
    int TotalRows,
    int UploadedRows,
    IReadOnlyList<BulkUploadRawMaterialRowError> Errors);

public record BulkUploadRawMaterialRowError(int RowNumber, string Message);

public class BulkUploadRawMaterialsValidator : AbstractValidator<BulkUploadRawMaterialsCommand>
{
    public BulkUploadRawMaterialsValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("File is required.");
    }
}

public class BulkUploadRawMaterialsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<BulkUploadRawMaterialsCommand, BulkUploadRawMaterialsResponse>
{
    // D-22's 10 MB ceiling with the bulk formats (spec 21.10) instead of the document list.
    private static readonly IReadOnlyList<string> AllowedExtensions = ["xls", "xlsx"];
    private const int MaxDataRows = 250; // spec 21.10

    // The Category literals of spec 5.1 / GeneralDataCatalog (PRODUCT group).
    private const string IngredientStatusCategory = "Ingredient Status";

    private const string IngredientSourceCategory = "Ingredient Source";

    private const string TemplateMismatch =
        "The file does not match the raw material bulk upload template.";

    // ExcelDataReader resolves legacy code pages (1252) when a reader is created; without
    // this provider Encoding.GetEncoding throws. Registered exactly once per process.
    static BulkUploadRawMaterialsHandler() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    // Which of the file's two halves a repeated header (COUNTRY, EMAIL, ...) belongs to.
    private enum Block
    {
        None,
        Supplier,
        Manufacturer
    }

    private enum Column
    {
        Ignore,
        Category,
        Ingredient,
        IngredientStatus,
        IngredientCode,
        CommercialName,
        ScientificName,
        IngredientSource,
        HalalCertified,
        SupplierName,
        SupplierAddress,
        SupplierCountry,
        SupplierBusinessRegistrationNo,
        SupplierPersonInCharge,
        SupplierContactNumber,
        SupplierEmail,
        ManufacturerName,
        ManufacturerAddress,
        ManufacturerCountry,
        ManufacturerBusinessRegistrationNo,
        ManufacturerPersonInCharge,
        ManufacturerContactNumber,
        ManufacturerEmail
    }

    public async Task<BulkUploadRawMaterialsResponse> Handle(
        BulkUploadRawMaterialsCommand request,
        CancellationToken ct)
    {
        if (request.File is not { } file)
            throw new BusinessRuleException("File is required.");

        FileValidation.Validate(file.FileName, file.Length, AllowedExtensions);

        var rows = await ReadRowsAsync(file); // rows[0] is the header row
        // The owner's template carries a sample row and a formatted but empty tail. Blank rows
        // are ignored entirely - they neither count nor produce errors - so an untouched
        // template reports "no data rows." instead of one error per empty line.
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
        var statuses = await LoadOptionsAsync(companyId, IngredientStatusCategory, ct);
        var sources = await LoadOptionsAsync(companyId, IngredientSourceCategory, ct);

        // Q16: the e-mail index of the caller's own rows - a hit is linked, never updated.
        var supplierByEmail = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var existing = await db.ManufacturerSuppliers.AsNoTracking()
            .Where(row => row.CompanyId == companyId)
            .Select(row => new { row.Id, row.ManufacturerEmail, row.SupplierEmail })
            .ToListAsync(ct);
        foreach (var row in existing)
        {
            if (row.ManufacturerEmail is { Length: > 0 })
                supplierByEmail.TryAdd(row.ManufacturerEmail, row.Id);
            if (row.SupplierEmail is { Length: > 0 })
                supplierByEmail.TryAdd(row.SupplierEmail, row.Id);
        }

        // D-17: the ingredient code is unique per company among live rows. A blank code is
        // stored as NULL and is never unique (D-17), so it is skipped here too.
        var usedCodes = (await db.RawMaterials.AsNoTracking()
                .Where(row => row.CompanyId == companyId && row.IngredientCode != null)
                .Select(row => row.IngredientCode!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<BulkUploadRawMaterialRowError>();
        var accepted = new List<RawMaterialEntity>();
        var createdSuppliers = new List<ManufacturerSupplierEntity>();
        foreach (var row in dataRows)
        {
            var outcome = ProcessRow(
                row, columns, companyId, countries, statuses, sources,
                usedCodes, supplierByEmail);
            if (outcome.Error is not null)
            {
                errors.Add(new BulkUploadRawMaterialRowError(row.RowNumber, outcome.Error));
                continue;
            }

            if (outcome.CreatedSupplier is { } created)
            {
                createdSuppliers.Add(created);
                if (created.ManufacturerEmail is not null)
                    supplierByEmail.TryAdd(created.ManufacturerEmail, created.Id);
                if (created.SupplierEmail is not null)
                    supplierByEmail.TryAdd(created.SupplierEmail, created.Id);
            }

            if (outcome.Entity!.IngredientCode is not null)
                usedCodes.Add(outcome.Entity.IngredientCode);
            accepted.Add(outcome.Entity);
        }

        if (accepted.Count > 0)
        {
            if (createdSuppliers.Count > 0)
                db.ManufacturerSuppliers.AddRange(createdSuppliers);
            db.RawMaterials.AddRange(accepted);

            // The file has no "Accessible For" column (Q6): Database.md 8 keeps >= 1 row, so
            // every material is shared with the caller's own company - the row the form would
            // pre-select as the owner itself.
            foreach (var material in accepted)
                db.RawMaterialAccessibleCompanies.Add(new RawMaterialAccessibleCompanyEntity
                {
                    RawMaterialId = material.Id,
                    AccessibleCompanyId = companyId
                });

            await db.SaveChangesAsync(ct);
        }

        return new BulkUploadRawMaterialsResponse(totalRows, accepted.Count, errors);
    }

    private sealed record SheetRow(int RowNumber, string[] Cells);

    private sealed record RowOutcome(
        string? Error,
        RawMaterialEntity? Entity,
        ManufacturerSupplierEntity? CreatedSupplier)
    {
        public static RowOutcome Failure(string error) => new(error, null, null);

        public static RowOutcome Success(
            RawMaterialEntity entity,
            ManufacturerSupplierEntity? created) => new(null, entity, created);
    }

    // The first worksheet (the templates name it Sheet1; spec 21.10 says "usually sheet1").
    // The header row is the first row naming both an ingredient and its status - rows 1-3 are
    // the legend and the group title ("INGREDIENTS/RAW MATERIALS INFORMATION") in the file.
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
                row.Cells.Any(cell =>
                    CellHeader(cell).Contains("INGREDIENT/RAW MATERIAL NAME"))
                && row.Cells.Any(cell => CellHeader(cell).Contains("INGREDIENT STATUS")));
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
    // markers removed, upper-cased. "RAW MATERIAL CATEGORY\n(Select from dropdown list only)"
    // keeps its dropdown hint, so every match below is a Contains.
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
        var block = Block.None;
        for (var i = 0; i < headerCells.Length; i++)
        {
            var header = CellHeader(headerCells[i]);

            // The two blocks are announced by their NAME column and the shared headers
            // (COUNTRY, BUSINESS REGISTRATION NO., PERSON IN CHARGE, CONTACT NUMBER, EMAIL)
            // follow inside their block - the template's order, which the required-column
            // check below pins down.
            if (header.Contains("SUPPLIER"))
                block = Block.Supplier;
            else if (header.Contains("MANUFACTURER"))
                block = Block.Manufacturer;

            var column = MatchColumn(header, block);
            if (column != Column.Ignore && !columns.ContainsKey(column))
                columns[column] = i;
        }

        return columns;
    }

    private static Column MatchColumn(string header, Block block) => header switch
    {
        var text when text.Contains("RAW MATERIAL CATEGORY") => Column.Category,
        var text when text.Contains("INGREDIENT/RAW MATERIAL NAME") => Column.Ingredient,
        var text when text.Contains("INGREDIENT STATUS") => Column.IngredientStatus,
        var text when text.Contains("INGREDIENT/RAW MATERIAL CODE") => Column.IngredientCode,
        var text when text.Contains("INGREDIENT/RAW MATERIAL SOURCE") =>
            Column.IngredientSource,
        var text when text.Contains("COMMERCIAL NAME") => Column.CommercialName,
        var text when text.Contains("SCIENTIFIC NAME") => Column.ScientificName,
        var text when text.Contains("HALAL CERTIFIED") => Column.HalalCertified,
        var text when text.Contains("NAME") =>
            BlockColumn(block, Column.SupplierName, Column.ManufacturerName),
        var text when text.Contains("ADDRESS") =>
            BlockColumn(block, Column.SupplierAddress, Column.ManufacturerAddress),
        var text when text.Contains("COUNTRY") =>
            BlockColumn(block, Column.SupplierCountry, Column.ManufacturerCountry),
        var text when text.Contains("BUSINESS REGISTRATION") => BlockColumn(
            block,
            Column.SupplierBusinessRegistrationNo,
            Column.ManufacturerBusinessRegistrationNo),
        var text when text.Contains("PERSON IN CHARGE") => BlockColumn(
            block,
            Column.SupplierPersonInCharge,
            Column.ManufacturerPersonInCharge),
        var text when text.Contains("CONTACT") =>
            BlockColumn(block, Column.SupplierContactNumber, Column.ManufacturerContactNumber),
        var text when text.Contains("EMAIL") =>
            BlockColumn(block, Column.SupplierEmail, Column.ManufacturerEmail),
        _ => Column.Ignore
    };

    private static Column BlockColumn(Block block, Column supplierColumn, Column manufacturerColumn) =>
        block switch
        {
            Block.Supplier => supplierColumn,
            Block.Manufacturer => manufacturerColumn,
            _ => Column.Ignore
        };

    // The columns the template marks mandatory (*) - plus the two dropdown columns the row
    // cannot be built without. HALAL CERTIFIED is required by the file but has no column in
    // Database.md (read, never stored), so it is deliberately not checked here.
    private static bool HasRequiredColumns(IReadOnlyDictionary<Column, int> columns) =>
        columns.ContainsKey(Column.Category)
        && columns.ContainsKey(Column.Ingredient)
        && columns.ContainsKey(Column.IngredientStatus)
        && columns.ContainsKey(Column.SupplierName)
        && columns.ContainsKey(Column.SupplierAddress)
        && columns.ContainsKey(Column.SupplierCountry)
        && columns.ContainsKey(Column.ManufacturerName)
        && columns.ContainsKey(Column.ManufacturerAddress)
        && columns.ContainsKey(Column.ManufacturerCountry)
        && columns.ContainsKey(Column.IngredientSource);

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

    // Everything the row needs is already loaded, so the whole pass is synchronous: validate
    // the cells (first failure wins), then link or build the manufacturer & supplier row.
    private RowOutcome ProcessRow(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        Guid companyId,
        IReadOnlyList<(Guid Id, string Name)> countries,
        IReadOnlyList<(Guid Id, string Name)> statuses,
        IReadOnlyList<(Guid Id, string Name)> sources,
        HashSet<string> usedCodes,
        IReadOnlyDictionary<string, Guid> supplierByEmail)
    {
        var category = Cell(row, columns, Column.Category);
        var ingredient = Cell(row, columns, Column.Ingredient);
        var statusName = Cell(row, columns, Column.IngredientStatus);
        var sourceName = Cell(row, columns, Column.IngredientSource);
        var code = Cell(row, columns, Column.IngredientCode);
        var commercialName = Cell(row, columns, Column.CommercialName);
        var scientificName = Cell(row, columns, Column.ScientificName);
        var supplierPresent = Cell(row, columns, Column.SupplierName).Length > 0;
        var manufacturerPresent = Cell(row, columns, Column.ManufacturerName).Length > 0;
        var status = Find(statuses, statusName);
        var source = Find(sources, sourceName);

        if (category.Length == 0)
            return RowOutcome.Failure("Category is required.");
        if (!TryParseCategory(category, out var parsedCategory))
            return RowOutcome.Failure("Category is invalid.");
        if (ingredient.Length == 0)
            return RowOutcome.Failure("Ingredient is required.");
        if (ingredient.Length > 200)
            return RowOutcome.Failure("Ingredient must be 200 characters or fewer.");
        if (statusName.Length == 0)
            return RowOutcome.Failure("Ingredient status is required.");
        if (status is null)
            return RowOutcome.Failure("Ingredient status not found.");
        if (!supplierPresent && !manufacturerPresent)
            return RowOutcome.Failure("Manufacturer is required.");
        if (code.Length > 50)
            return RowOutcome.Failure("Ingredient code must be 50 characters or fewer.");
        if (code.Length > 0 && usedCodes.Contains(code))
            return RowOutcome.Failure("An ingredient with this code already exists.");
        if (commercialName.Length > 200)
            return RowOutcome.Failure("Commercial name must be 200 characters or fewer.");
        if (scientificName.Length > 200)
            return RowOutcome.Failure("Scientific name must be 200 characters or fewer.");
        if (sourceName.Length == 0)
            return RowOutcome.Failure("Ingredient source is required.");
        if (source is null)
            return RowOutcome.Failure("Ingredient source not found.");

        var (supplierError, supplierId, created) = ResolveSupplier(
            row, columns, companyId, countries,
            supplierPresent, manufacturerPresent, supplierByEmail);
        if (supplierError is not null)
            return RowOutcome.Failure(supplierError);

        var entity = RawMaterialData.Apply(
            new RawMaterialEntity
            {
                CompanyId = companyId
            },
            new CreateRawMaterialCommand(
                parsedCategory,
                status.Value.Id,
                ingredient,
                NullIfBlank(code),
                NullIfBlank(commercialName),
                NullIfBlank(scientificName),
                source.Value.Id,
                supplierId,
                false,
                null));

        return RowOutcome.Success(entity, created);
    }

    // Q16 (owner answered): link the caller's own live row when either block's e-mail names
    // it - the supplier block comes first in the template, so its e-mail is tried first and a
    // hit wins over the manufacturer's. Nothing matches (or no e-mail at all) builds a new
    // row, whose blocks are then checked against the Create rules - a reused row keeps its
    // own data, so the file's block values are informational for it.
    private static (string? Error, Guid Id, ManufacturerSupplierEntity? Created)
        ResolveSupplier(
            SheetRow row,
            IReadOnlyDictionary<Column, int> columns,
            Guid companyId,
            IReadOnlyList<(Guid Id, string Name)> countries,
            bool supplierPresent,
            bool manufacturerPresent,
            IReadOnlyDictionary<string, Guid> supplierByEmail)
    {
        foreach (var email in MatchEmails(
                     row, columns, supplierPresent, manufacturerPresent))
            if (supplierByEmail.TryGetValue(email, out var existingId))
                return (null, existingId, null);

        if (supplierPresent)
        {
            var supplierError = ValidateBlock(
                "Supplier",
                Cell(row, columns, Column.SupplierName),
                Cell(row, columns, Column.SupplierAddress),
                Cell(row, columns, Column.SupplierCountry),
                Cell(row, columns, Column.SupplierPersonInCharge),
                Cell(row, columns, Column.SupplierContactNumber),
                Cell(row, columns, Column.SupplierEmail),
                Cell(row, columns, Column.SupplierBusinessRegistrationNo),
                countries);
            if (supplierError is not null)
                return (supplierError, default, null);
        }

        if (manufacturerPresent)
        {
            var manufacturerError = ValidateBlock(
                "Manufacturer",
                Cell(row, columns, Column.ManufacturerName),
                Cell(row, columns, Column.ManufacturerAddress),
                Cell(row, columns, Column.ManufacturerCountry),
                Cell(row, columns, Column.ManufacturerPersonInCharge),
                Cell(row, columns, Column.ManufacturerContactNumber),
                Cell(row, columns, Column.ManufacturerEmail),
                Cell(row, columns, Column.ManufacturerBusinessRegistrationNo),
                countries);
            if (manufacturerError is not null)
                return (manufacturerError, default, null);
        }

        var type = supplierPresent && manufacturerPresent
            ? ManufacturerSupplierType.Both
            : manufacturerPresent
                ? ManufacturerSupplierType.ManufacturerOnly
                : ManufacturerSupplierType.SupplierOnly;

        // Type is derived from which blocks are present, so the Apply clears exactly the
        // absent half - the row is identical to what the 10.1 form would store. The raw
        // material template carries neither a Category nor a Webpage column (Q6): both stay
        // empty, and the form makes Category optional anyway.
        var created = ManufacturerSupplierData.Apply(
            new ManufacturerSupplierEntity
            {
                CompanyId = companyId
            },
            new CreateManufacturerSupplierCommand(
                type,
                Cell(row, columns, Column.ManufacturerName),
                NullIfBlank(Cell(row, columns, Column.ManufacturerBusinessRegistrationNo)),
                null,
                NullIfBlank(Cell(row, columns, Column.ManufacturerAddress)),
                CountryIdOf(
                    countries, Cell(row, columns, Column.ManufacturerCountry),
                    manufacturerPresent),
                NullIfBlank(Cell(row, columns, Column.ManufacturerPersonInCharge)),
                NullIfBlank(Cell(row, columns, Column.ManufacturerContactNumber)),
                NullIfBlank(Cell(row, columns, Column.ManufacturerEmail)),
                null,
                Cell(row, columns, Column.SupplierName),
                NullIfBlank(Cell(row, columns, Column.SupplierAddress)),
                CountryIdOf(
                    countries, Cell(row, columns, Column.SupplierCountry), supplierPresent),
                NullIfBlank(Cell(row, columns, Column.SupplierPersonInCharge)),
                NullIfBlank(Cell(row, columns, Column.SupplierContactNumber)),
                NullIfBlank(Cell(row, columns, Column.SupplierEmail))));

        return (null, created.Id, created);
    }

    // The half of a row that exists (Q16) is a whole record: name, address and country are
    // mandatory on the 10.1 form and the template's "N/A" cells are already blank here.
    private static string? ValidateBlock(
        string prefix,
        string name,
        string address,
        string country,
        string personInCharge,
        string contactNumber,
        string email,
        string businessRegistrationNo,
        IReadOnlyList<(Guid Id, string Name)> countries)
    {
        if (name.Length > 200)
            return $"{prefix} name must be 200 characters or fewer.";
        if (address.Length == 0)
            return $"{prefix} address is required.";
        if (address.Length > 500)
            return $"{prefix} address must be 500 characters or fewer.";
        if (country.Length == 0)
            return "Country is required.";
        if (!countries.Any(entry => SameName(entry.Name, country)))
            return "Country not found.";
        if (personInCharge.Length > 200)
            return "Person in charge must be 200 characters or fewer.";
        if (contactNumber.Length > 30)
            return "Contact number must be 30 characters or fewer.";
        if (email.Length > 254)
            return "E-mail must be 254 characters or fewer.";
        if (businessRegistrationNo.Length > 50)
            return "Business registration number must be 50 characters or fewer.";
        return null;
    }

    private static IEnumerable<string> MatchEmails(
        SheetRow row,
        IReadOnlyDictionary<Column, int> columns,
        bool supplierPresent,
        bool manufacturerPresent)
    {
        if (supplierPresent)
        {
            var email = Cell(row, columns, Column.SupplierEmail);
            if (email.Length > 0)
                yield return email;
        }

        if (manufacturerPresent)
        {
            var email = Cell(row, columns, Column.ManufacturerEmail);
            if (email.Length > 0)
                yield return email;
        }
    }

    private static Guid? CountryIdOf(
        IReadOnlyList<(Guid Id, string Name)> countries,
        string country,
        bool blockPresent) =>
        !blockPresent || country.Length == 0
            ? null
            : countries.First(entry => SameName(entry.Name, country)).Id;

    private static (Guid Id, string Name)? Find(
        IReadOnlyList<(Guid Id, string Name)> options,
        string name)
    {
        if (name.Length == 0)
            return null;
        foreach (var option in options)
            if (SameName(option.Name, name))
                return option;
        return null;
    }

    private async Task<List<(Guid Id, string Name)>> LoadOptionsAsync(
        Guid companyId,
        string category,
        CancellationToken ct) =>
        (await db.GeneralData.AsNoTracking()
                .Where(row => row.CompanyId == companyId && row.Category == category)
                .Select(row => new { row.Id, row.Name })
                .ToListAsync(ct))
            .Select(row => (row.Id, row.Name))
            .ToList();

    // The Category literals of the spec form / template dropdown: Core and Supporting (the
    // file writes "Supporting Material").
    private static bool TryParseCategory(string value, out RawMaterialCategory category)
    {
        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        switch (normalized)
        {
            case "core":
                category = RawMaterialCategory.Core;
                return true;
            case "supporting":
            case "supportingmaterial":
                category = RawMaterialCategory.Supporting;
                return true;
            default:
                category = default;
                return false;
        }
    }

    private static bool SameName(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? NullIfBlank(string value) =>
        value.Length == 0 ? null : value;
}
