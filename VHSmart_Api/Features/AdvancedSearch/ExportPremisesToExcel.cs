using ClosedXML.Excel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.AdvancedSearch;

// Advanced Search > "Export to Excel" for the Premise view (spec 8): the SAME 19 filters as
// Search, over the same query - the file always matches what the grid would return. One row
// per premise with the numbered Halal Information list serialized as newline-separated text
// in one cell (the spec never spells the export layout - ours, flagged for the owner). The
// legacy defect of an unauthenticated, view-name-driven export [CODE] is not replicated: the
// endpoint is permission-gated and hard-wired to this screen.
public record ExportPremisesToExcelQuery
    : PremiseSearchFilters, IRequest<ExportPremisesToExcelResponse>;

public record ExportPremisesToExcelResponse(
    byte[] Content,
    string ContentType,
    string FileName);

public class ExportPremisesToExcelHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<ExportPremisesToExcelQuery, ExportPremisesToExcelResponse>
{
    private static readonly string[] Headers =
    [
        "Store Code",
        "Establishment Name",
        "Address",
        "Phone No.",
        "Email",
        "View Premise Location",
        "Brand",
        "Menu Concept",
        "Area Manager",
        "Operation Manager",
        "Halal Information",
        "Premise Document Status",
        "Premise Tag"
    ];

    public async Task<ExportPremisesToExcelResponse> Handle(
        ExportPremisesToExcelQuery request,
        CancellationToken ct)
    {
        var baseQuery = await PremiseSearch.BuildQueryAsync(db, user, request, ct);
        var rows = await PremiseSearch
            .Project(baseQuery, db)
            .ApplySort(nameof(SearchPremisesResponse.StoreName), sortDescending: false)
            .ToListAsync(ct);

        var ids = rows.Select(row => row.Id).ToList();
        var today = PremiseDocumentClock.Today();
        var documentStatuses = await PremiseSearch.DocumentStatusesAsync(db, ids, today, ct);
        var halalByPremise = await PremiseHalalInfoLoader.LoadAsync(db, user, ids, ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Premise");
        for (var column = 0; column < Headers.Length; column++)
            sheet.Cell(1, column + 1).Value = Headers[column];

        var number = 1;
        foreach (var row in rows)
        {
            number++;
            var halalInformation = halalByPremise.GetValueOrDefault(row.Id) ?? [];
            var values = new[]
            {
                row.StoreCode,
                row.StoreName,
                row.Address,
                row.Telephone,
                row.Email,
                row.PremiseLocationUrl ?? string.Empty,
                row.Brand,
                row.MenuConcept,
                row.AreaManager,
                row.OperationManager,
                string.Join(Environment.NewLine, halalInformation.Select((info, index) =>
                    $"{index + 1}. {info.VhSmartReferenceNo}"
                    + $" | {info.CbReferenceNo ?? string.Empty}"
                    + $" | {info.Scheme}"
                    + $" | {info.ApplicationStatus}"
                    + $" | {info.HalalCertificateNo ?? string.Empty}"
                    + $" | {info.HalalCertificateStatus?.ToString() ?? string.Empty}"
                    + $" | {info.HalalExpiryDate?.ToString("dd'/'MM'/'yyyy") ?? string.Empty}")),
                documentStatuses[row.Id],
                row.PremiseTag
            };
            for (var column = 0; column < values.Length; column++)
                sheet.Cell(number, column + 1).Value = values[column];
        }

        sheet.Columns(1, Headers.Length).AdjustToContents();
        sheet.Column(11).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExportPremisesToExcelResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"AdvancedSearch-Premise-{today:yyyyMMdd}.xlsx");
    }
}

public class ExportPremisesValidator : AbstractValidator<ExportPremisesToExcelQuery>
{
    public ExportPremisesValidator()
    {
        PremiseSearchFilterRules.Apply(this);
    }
}
