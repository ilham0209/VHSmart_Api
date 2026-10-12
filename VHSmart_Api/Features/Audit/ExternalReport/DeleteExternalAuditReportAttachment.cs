using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Delete one attachment (soft delete; the bytes stay in storage - the house stance of
// every attachment delete). The row must belong to a live own-company report;
// unknown/foreign -> 404.
public record DeleteExternalAuditReportAttachmentCommand(Guid Id) : IRequest;

public class DeleteExternalAuditReportAttachmentValidator
    : AbstractValidator<DeleteExternalAuditReportAttachmentCommand>
{
    public DeleteExternalAuditReportAttachmentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class DeleteExternalAuditReportAttachmentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteExternalAuditReportAttachmentCommand>
{
    public async Task Handle(
        DeleteExternalAuditReportAttachmentCommand request,
        CancellationToken ct)
    {
        var attachment = await db.ExternalAuditReportAttachments
            .FirstOrDefaultAsync(row =>
                row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Attachment not found.");

        var reportOwned = await ExternalReportQueries.Own(db, user)
            .AsNoTracking()
            .AnyAsync(row => row.Id == attachment.ExternalAuditReportId, ct);
        if (!reportOwned)
        {
            throw new NotFoundException("Attachment not found.");
        }

        db.ExternalAuditReportAttachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
    }
}
