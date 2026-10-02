using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Minutes Meeting list (spec 7.6, CONFIRMED columns): Action (client side - only the Id is
// needed), Title, Date, Start Time, End Time, Location, Company. Search covers title and
// location like the other lists; the default order is MeetingDate descending (newest first) -
// the spec does not state a default, the same choice P-02 made for its date list. The
// explicit CompanyId match keeps a ViewAllCompanies caller on their own rows.
public record GetAllMinutesMeetingsQuery : IRequest<DataGridResponse<GetAllMinutesMeetingsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllMinutesMeetingsResponse(
    Guid Id,
    string Title,
    DateTime MeetingDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    string Company);

public class GetAllMinutesMeetingsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllMinutesMeetingsQuery, DataGridResponse<GetAllMinutesMeetingsResponse>>
{
    public async Task<DataGridResponse<GetAllMinutesMeetingsResponse>> Handle(
        GetAllMinutesMeetingsQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllMinutesMeetingsResponse.MeetingDate)
            : request.Request.SortBy;
        var sortDescending = defaultSort || request.Request.SortDescending;

        return await (
                from meeting in db.MinutesMeetings.AsNoTracking()
                    .Where(x => x.CompanyId == user.CompanyId)
                select new GetAllMinutesMeetingsResponse(
                    meeting.Id,
                    meeting.Title,
                    meeting.MeetingDate,
                    meeting.StartTime,
                    meeting.EndTime,
                    meeting.Location,
                    (from company in db.Companies
                     where company.Id == meeting.CompanyId
                     select company.Name).FirstOrDefault() ?? string.Empty))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllMinutesMeetingsResponse.Title),
                nameof(GetAllMinutesMeetingsResponse.Location))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
