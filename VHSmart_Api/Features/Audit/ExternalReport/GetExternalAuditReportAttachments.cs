using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Tab "Attachment" list (spec 14.10: the tab was "not seen" in the manual, Database.md 13
// defines the table): # + File Name + client-side Action, the staff-attachment stance.
// Behind Audit.ExternalReport View.
public record GetExternalAuditReportAttachmentsQuery(Guid Id)
    : IRequest<IReadOnlyList<ExternalAuditReportAttachmentResponse>>;

public record ExternalAuditReportAttachmentResponse(
    Guid Id,
    string FileName,
    long SizeBytes);

public class GetExternalAuditReportAttachmentsValidator
    : AbstractValidator<GetExternalAuditReportAttachmentsQuery>
{
    public GetExternalAuditReportAttachmentsValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetExternalAuditReportAttachmentsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetExternalAuditReportAttachmentsQuery, IReadOnlyList<ExternalAuditReportAttachmentResponse>>
{
    public async Task<IReadOnlyList<ExternalAuditReportAttachmentResponse>> Handle(
        GetExternalAuditReportAttachmentsQuery request,
        CancellationToken ct)
    {
        _ = await ExternalReportQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("External audit report not found.");

        return await db.ExternalAuditReportAttachments.AsNoTracking()
            .Where(row => row.ExternalAuditReportId == request.Id)
            .OrderBy(row => row.SysDateCreated)
            .ThenBy(row => row.Id)
            .Select(row => new ExternalAuditReportAttachmentResponse(
                row.Id,
                row.Document.FileName,
                row.Document.SizeBytes))
            .ToListAsync(ct);
    }
}
