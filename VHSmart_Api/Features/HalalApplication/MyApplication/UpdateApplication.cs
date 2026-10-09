using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Header save of the application screen (spec 12.5): Application Type, CB Application No.,
// CB Application Date, Halal Coach and Batch Selection - the header's own save buttons all
// land here (full update of the header block, same stance as UpdateBatch). CB number/date
// are entered before submit (HA-04 requires them); the batch must be one of the caller's
// own. D-26: submitted applications are read-only. Identity comes from the JWT.
public record UpdateApplicationCommand(
    Guid Id,
    string ApplicationType,
    string? CbApplicationNo,
    DateOnly? CbApplicationDate,
    string? HalalCoachName,
    Guid? BatchId) : IRequest<ApplicationHeaderResponse>;

public record ApplicationHeaderResponse(
    Guid Id,
    string Status,
    string ApplicationType,
    string? CbApplicationNo,
    DateOnly? CbApplicationDate,
    string? HalalCoachName,
    Guid? BatchId,
    string? BatchName);

public class UpdateApplicationValidator : AbstractValidator<UpdateApplicationCommand>
{
    public UpdateApplicationValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.ApplicationType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Application type is required.")
            .MaximumLength(20).WithMessage("Application type must be 20 characters or fewer.")
            .Must(value =>
                string.Equals(value, "New", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Renewal", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Application type must be New or Renewal.");

        RuleFor(x => x.CbApplicationNo).MaximumLength(100)
            .WithMessage("CB application number must be 100 characters or fewer.");

        RuleFor(x => x.HalalCoachName).MaximumLength(200)
            .WithMessage("Halal coach must be 200 characters or fewer.");

        // The batch dropdown lists the caller's own batches only, so anything else is a
        // foreign or unknown row -> 400 with our message (never a 403).
        RuleFor(x => x.BatchId)
            .MustAsync(async (id, ct) => id is null || await db.Batches.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Batch not found.");
    }
}

public class UpdateApplicationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateApplicationCommand, ApplicationHeaderResponse>
{
    public async Task<ApplicationHeaderResponse> Handle(
        UpdateApplicationCommand request,
        CancellationToken ct)
    {
        var application = await db.Applications
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Application not found.");

        ApplicationData.EnsureDraft(application);

        application.ApplicationType = ApplicationData.NormalizeApplicationType(
            request.ApplicationType);
        application.CbApplicationNo = request.CbApplicationNo;
        application.CbApplicationDate = request.CbApplicationDate;
        application.HalalCoachName = request.HalalCoachName;
        application.BatchId = request.BatchId;
        await db.SaveChangesAsync(ct);

        var batchName = request.BatchId is null
            ? null
            : await db.Batches.AsNoTracking()
                .Where(row => row.Id == request.BatchId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct);

        return new ApplicationHeaderResponse(
            application.Id,
            application.Status,
            application.ApplicationType,
            application.CbApplicationNo,
            application.CbApplicationDate,
            application.HalalCoachName,
            application.BatchId,
            batchName);
    }
}
