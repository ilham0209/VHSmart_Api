using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// "Tagging Application Status" button of the application list (spec 12.7 [MANUAL], D-26):
// the user moves a SUBMITTED application through the CB-progress statuses - Application
// Approved, Audit (In Progress), Audit (Completed), Approved with Document - "in that order
// or any later one", so a target may never sit behind the current status (skipping ahead is
// allowed, going back is not - our reading of D-26, flagged). DRAFT is not taggable: only
// Submit leaves the draft. Every change writes AppApplicationStatusHistories (D-26) and
// updates Status/StatusDate on the row itself; the first crossing of APPLICATION APPROVED
// also snapshots the batch into AppCertificateItems (Database.md 10, HA-06 helper), and
// tagging is one of the D-26 read-only exceptions, so no EnsureDraft runs. Identity comes
// from the JWT.
public record TagApplicationStatusCommand(
    Guid Id,
    string Status,
    string? Remarks) : IRequest<ApplicationStatusTagResponse>;

// Tag response: the two list columns (spec 12.3) the Save refreshes.
public record ApplicationStatusTagResponse(
    Guid Id,
    string Status,
    DateTime StatusDate);

public class TagApplicationStatusValidator : AbstractValidator<TagApplicationStatusCommand>
{
    public TagApplicationStatusValidator()
    {
        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .Must(status => ApplicationData.CanonicalTaggingStatus(status) is not null)
            .WithMessage(
                "Status must be Application Approved, Audit (In Progress), "
                + "Audit (Completed) or Approved with Document.");

        RuleFor(x => x.Remarks)
            .MaximumLength(500).WithMessage("Remarks must be 500 characters or fewer.");
    }
}

public class TagApplicationStatusHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<TagApplicationStatusCommand, ApplicationStatusTagResponse>
{
    public async Task<ApplicationStatusTagResponse> Handle(
        TagApplicationStatusCommand request,
        CancellationToken ct)
    {
        var application = await db.Applications
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Application not found.");

        // Only Submit moves an application out of DRAFT (D-26), so a draft has no CB
        // progress to tag yet. The message is ours (the spec gives none).
        if (application.Status == ApplicationStatus.Draft)
            throw new BusinessRuleException(
                "Submit the application before tagging its status.");

        var currentIndex = ApplicationData.TaggingSequenceIndex(application.Status);
        if (currentIndex < 0)
            throw new BusinessRuleException("This application status cannot be tagged.");

        // The validator already accepts only the four; the handler re-canonicalizes so a
        // casing trick can never store a value outside D-26.
        var target = ApplicationData.CanonicalTaggingStatus(request.Status)
            ?? throw new BusinessRuleException(
                "Status must be Application Approved, Audit (In Progress), "
                + "Audit (Completed) or Approved with Document.");
        var targetIndex = ApplicationData.TaggingSequenceIndex(target);
        if (targetIndex < currentIndex)
            throw new BusinessRuleException("Application status can only move forward.");

        var now = DateTime.UtcNow;
        var changedBy = Guid.TryParse(user.UserId, out var parsed) ? parsed : (Guid?)null;
        var previousStatus = application.Status;
        application.Status = target;
        application.StatusDate = now;

        // Database.md 10: AppCertificateItems are created when the app becomes approved -
        // the first crossing of the APPLICATION APPROVED threshold, a skip straight past it
        // included (D-26 allows a later status directly). HA-06 owns the table; re-tags of
        // an already-approved status are idempotent inside the helper.
        var approvedIndex = ApplicationData.TaggingSequenceIndex(
            ApplicationStatus.ApplicationApproved);
        if (currentIndex < approvedIndex && targetIndex >= approvedIndex)
            await CertificateItemCreation.CreateOnApprovalAsync(db, application, ct);

        // D-26: every status change writes AppApplicationStatusHistories.
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistoryEntity
        {
            CompanyId = application.CompanyId,
            ApplicationId = application.Id,
            FromStatus = previousStatus,
            ToStatus = target,
            ChangedAt = now,
            ChangedBy = changedBy,
            Remarks = request.Remarks
        });
        await db.SaveChangesAsync(ct);

        return new ApplicationStatusTagResponse(
            application.Id, application.Status, application.StatusDate);
    }
}
