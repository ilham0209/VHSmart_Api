using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.NonConformanceList;

// "Fill by Auditor" save of the Manage Corrective Action Details page (spec 14.11
// [MANUAL]): Comment* plus the Status dropdown. D-05 defines the auditor's transition
// CorrectiveActionSubmitted -> Closed (reopen not allowed), so Closed is the only value
// the dropdown can set and the NC must be in CorrectiveActionSubmitted first; every other
// status is a validation failure (flagged: the full dropdown value list is [VERIFY], D-05
// is the default). Each save keeps a versioned append-only entry like the auditee's.
// Closed stamps ClosedAt (server UTC, D-24) and ClosedBy (JWT user id). Behind
// Audit.NonConformance Edit.
public record ReviewCorrectiveActionCommand(
    Guid Id,
    string Comment,
    string StatusSet) : IRequest<CorrectiveActionEntryResponse>;

public class ReviewCorrectiveActionValidator : AbstractValidator<ReviewCorrectiveActionCommand>
{
    public ReviewCorrectiveActionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Comment is required.")
            .MaximumLength(2000)
            .WithMessage("Comment cannot exceed 2000 characters.");

        RuleFor(x => x.StatusSet)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .Must(status => status == NonConformanceStatus.Closed)
            .WithMessage("Status must be Closed.");
    }
}

public class ReviewCorrectiveActionHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<ReviewCorrectiveActionCommand, CorrectiveActionEntryResponse>
{
    public async Task<CorrectiveActionEntryResponse> Handle(
        ReviewCorrectiveActionCommand request,
        CancellationToken ct)
    {
        var nc = await NonConformanceQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Non-conformance not found.");

        if (nc.Status == NonConformanceStatus.Closed)
        {
            throw new ConflictException("The non-conformance is closed.");
        }

        if (nc.Status != NonConformanceStatus.CorrectiveActionSubmitted)
        {
            throw new ConflictException("The corrective action has not been submitted yet.");
        }

        if (!Guid.TryParse(user.UserId, out var userId))
        {
            throw new BusinessRuleException("User id is invalid.");
        }

        var version = await SaveCorrectiveActionHandler.NextVersionAsync(db, nc.Id, ct);
        var entry = new CorrectiveActionEntryEntity
        {
            CompanyId = user.CompanyId,
            NonConformanceId = nc.Id,
            FilledBy = CorrectiveActionFilledBy.Auditor,
            Comment = request.Comment,
            StatusSet = NonConformanceStatus.Closed,
            Version = version,
            FilledByUserId = userId
        };
        db.CorrectiveActionEntries.Add(entry);

        nc.Status = NonConformanceStatus.Closed;
        nc.ClosedAt = DateTime.UtcNow;
        nc.ClosedBy = userId;

        await db.SaveChangesAsync(ct);

        return new CorrectiveActionEntryResponse(
            entry.Id,
            nc.Id,
            entry.FilledBy.ToString(),
            entry.Details,
            entry.Comment,
            entry.StatusSet,
            entry.Version,
            entry.Attachment?.FileName,
            entry.Attachment?.SizeBytes);
    }
}
