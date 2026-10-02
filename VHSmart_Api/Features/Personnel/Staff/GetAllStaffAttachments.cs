using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Personnel.Staff;

// The "Staff Attachment" tab of the Manage Staff modal (spec 7.4): table #, Type of Document,
// File Name, Action (the Action cell is client side and only needs the Id). The staff row is
// loaded first with the explicit CompanyId match, so another tenant's staff answers 404 and
// never leaks its files (CodingRules 9). Search covers the document type and the file name;
// default order is the document type ascending (the column order of the tab), "#" renumbers
// over the whole list.
public record GetAllStaffAttachmentsQuery(Guid StaffId, DataGridRequest Request)
    : IRequest<DataGridResponse<GetAllStaffAttachmentsResponse>>;

public record GetAllStaffAttachmentsResponse(
    Guid Id,
    int No,
    Guid StaffId,
    Guid DocumentTypeId,
    string DocumentType,
    string FileName);

public class GetAllStaffAttachmentsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllStaffAttachmentsQuery, DataGridResponse<GetAllStaffAttachmentsResponse>>
{
    public async Task<DataGridResponse<GetAllStaffAttachmentsResponse>> Handle(
        GetAllStaffAttachmentsQuery request,
        CancellationToken ct)
    {
        var owned = await db.Staffs.AsNoTracking()
            .AnyAsync(row => row.Id == request.StaffId && row.CompanyId == user.CompanyId, ct);
        if (!owned)
            throw new NotFoundException("Staff not found.");

        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllStaffAttachmentsResponse.DocumentType)
            : request.Request.SortBy;

        var response = await (
                from attachment in db.StaffAttachments.AsNoTracking()
                    .Where(x => x.StaffId == request.StaffId && x.CompanyId == user.CompanyId)
                select new GetAllStaffAttachmentsResponse(
                    attachment.Id,
                    0,
                    attachment.StaffId,
                    attachment.DocumentTypeId,
                    attachment.DocumentType == null
                        ? string.Empty
                        : attachment.DocumentType.DocumentType,
                    attachment.Document.FileName))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllStaffAttachmentsResponse.DocumentType),
                nameof(GetAllStaffAttachmentsResponse.FileName))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        var offset = (response.CurrentPage - 1) * response.PageSize;
        response.Data = response.Data
            .Select((row, index) => row with { No = offset + index + 1 })
            .ToList();

        return response;
    }
}
