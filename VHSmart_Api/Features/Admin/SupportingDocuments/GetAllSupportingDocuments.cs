using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// Supporting Document list (spec 5.5 "Show N entries" table): Document Type, For View,
// Description, Mandatory (Yes/No on screen - the API sends the bool), Document Sequence,
// Modified Date. The Action column (view / edit / delete) is client-side only. [T] table, so
// the global CompanyId filter scopes the rows (spec 3.3, Database.md 3).
public record GetAllSupportingDocumentsQuery
    : IRequest<DataGridResponse<GetAllSupportingDocumentsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllSupportingDocumentsResponse(
    Guid Id,
    string DocumentType,
    SupportingDocumentForView ForView,
    string? Description,
    bool IsMandatory,
    int DocumentSequence,
    DateTime? ModifiedDate);

public class GetAllSupportingDocumentsHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllSupportingDocumentsQuery, DataGridResponse<GetAllSupportingDocumentsResponse>>
{
    public async Task<DataGridResponse<GetAllSupportingDocumentsResponse>> Handle(
        GetAllSupportingDocumentsQuery request,
        CancellationToken ct) =>
        await db.SupportingDocuments
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(SupportingDocumentEntity.DocumentType),
                nameof(SupportingDocumentEntity.Description))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(row => new GetAllSupportingDocumentsResponse(
                row.Id,
                row.DocumentType,
                row.ForView,
                row.Description,
                row.IsMandatory,
                row.DocumentSequence,
                row.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);
}
