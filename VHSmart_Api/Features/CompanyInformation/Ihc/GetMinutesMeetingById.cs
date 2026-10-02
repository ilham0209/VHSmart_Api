using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Detail of one minutes meeting - the state the meeting form opens with (spec 7.6: tabs
// "Meeting Details" and "Attachment"; the attachment tab lists #, File Name, Action). An
// unknown or foreign meeting answers 404 (never 403). The same loader builds the response
// after Create/Update/Upload, so one code path defines the shape.
public record GetMinutesMeetingByIdQuery(Guid Id) : IRequest<MinutesMeetingDetailResponse>;

public record MinutesMeetingDetailResponse(
    Guid Id,
    string Title,
    string Company,
    DateTime MeetingDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    IReadOnlyList<MinutesMeetingAttachmentResponse> Attachments);

public record MinutesMeetingAttachmentResponse(
    Guid Id,
    int No,
    string? FileName);

public class GetMinutesMeetingByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetMinutesMeetingByIdQuery, MinutesMeetingDetailResponse>
{
    public Task<MinutesMeetingDetailResponse> Handle(
        GetMinutesMeetingByIdQuery request,
        CancellationToken ct) =>
        MinutesMeetingResponseLoader.LoadAsync(db, user.CompanyId, request.Id, ct);
}

internal static class MinutesMeetingResponseLoader
{
    public static async Task<MinutesMeetingDetailResponse> LoadAsync(
        VHSmartDbContext db,
        Guid companyId,
        Guid minutesMeetingId,
        CancellationToken ct)
    {
        var meeting = await db.MinutesMeetings
            .AsNoTracking()
            .Where(row => row.Id == minutesMeetingId && row.CompanyId == companyId)
            .Select(row => new
            {
                row.Id,
                row.Title,
                row.MeetingDate,
                row.StartTime,
                row.EndTime,
                row.Location
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Minutes meeting not found.");

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(row => row.Id == companyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        // The tenant filter on the attachment table only sees the caller's live rows.
        var attachments = await db.MinutesMeetingAttachments
            .AsNoTracking()
            .Where(row => row.MinutesMeetingId == minutesMeetingId)
            .OrderBy(row => row.SysDateCreated)
            .ThenBy(row => row.Id)
            .Select(row => new
            {
                row.Id,
                FileName = row.Document == null ? null : row.Document.FileName
            })
            .ToListAsync(ct);

        return new MinutesMeetingDetailResponse(
            meeting.Id,
            meeting.Title,
            companyName,
            meeting.MeetingDate,
            meeting.StartTime,
            meeting.EndTime,
            meeting.Location,
            [.. attachments.Select((row, index) => new MinutesMeetingAttachmentResponse(
                row.Id,
                index + 1,
                row.FileName))]);
    }
}
