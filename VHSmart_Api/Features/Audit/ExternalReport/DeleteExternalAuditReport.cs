using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Delete of a report (spec 14.10 list Action). Soft delete all round (CodingRules 7.1),
// children first the AU-06 way: NC rows, attachments (bytes kept - house stance), the
// premise links, then the header. Unknown/foreign -> 404.
public record DeleteExternalAuditReportCommand(Guid Id) : IRequest;

public class DeleteExternalAuditReportValidator : AbstractValidator<DeleteExternalAuditReportCommand>
{
    public DeleteExternalAuditReportValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class DeleteExternalAuditReportHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteExternalAuditReportCommand>
{
    public async Task Handle(DeleteExternalAuditReportCommand request, CancellationToken ct)
    {
        var report = await ExternalReportQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("External audit report not found.");

        var nonConformances = await db.NonConformances
            .Where(row => row.ExternalAuditReportId == report.Id)
            .ToListAsync(ct);
        foreach (var nonConformance in nonConformances)
        {
            db.NonConformances.Remove(nonConformance);
        }

        var attachments = await db.ExternalAuditReportAttachments
            .Where(row => row.ExternalAuditReportId == report.Id)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
        {
            db.ExternalAuditReportAttachments.Remove(attachment);
        }

        var premiseLinks = await db.ExternalAuditReportPremises
            .Where(row => row.ExternalAuditReportId == report.Id)
            .ToListAsync(ct);
        foreach (var premiseLink in premiseLinks)
        {
            db.ExternalAuditReportPremises.Remove(premiseLink);
        }

        db.ExternalAuditReports.Remove(report);

        await db.SaveChangesAsync(ct);
    }
}
