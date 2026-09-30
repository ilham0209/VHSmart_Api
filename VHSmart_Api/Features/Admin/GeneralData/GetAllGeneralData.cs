using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.GeneralData;

// General Data list (spec 5.1 "Show N entries" table): Group and Category dropdown filters
// (both optional - "-- PLEASE SELECT --"), Search box, sortable columns, paging. Rows of other
// companies never appear: the tenant filter is on the context (spec 3.3).
public record GetAllGeneralDataQuery : IRequest<DataGridResponse<GetAllGeneralDataResponse>>
{
    public DataGridRequest Request { get; set; } = new();

    public GeneralDataGroup? Group { get; set; }

    public string? Category { get; set; }
}

// Column "Modified Date" is SysDateModified (never modified since creation = null). Sort keys
// are entity property names, so the Modified Date column sorts on SysDateModified.
public record GetAllGeneralDataResponse(
    Guid Id,
    GeneralDataGroup Group,
    string Category,
    string Name,
    string? Description,
    DateTime? ModifiedDate);

public class GetAllGeneralDataHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllGeneralDataQuery, DataGridResponse<GetAllGeneralDataResponse>>
{
    public async Task<DataGridResponse<GetAllGeneralDataResponse>> Handle(
        GetAllGeneralDataQuery request,
        CancellationToken ct)
    {
        var query = db.GeneralData.AsNoTracking();

        if (request.Group is { } group)
            query = query.Where(row => row.Group == group);

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category;
            query = query.Where(row => row.Category == category);
        }

        return await query
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GeneralDataEntity.Category),
                nameof(GeneralDataEntity.Name),
                nameof(GeneralDataEntity.Description))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(row => new GetAllGeneralDataResponse(
                row.Id, row.Group, row.Category, row.Name, row.Description, row.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
