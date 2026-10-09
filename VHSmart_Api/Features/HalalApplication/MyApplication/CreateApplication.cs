using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Sequences;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// "+ New Application" (spec 12.3): pick a scheme, answer the Application Survey Form
// (12.4), receive the VH SMART Reference No. (D-09: {Prefix}({SchemeCode})/{ddMMyyyy}/{n}
// through IReferenceNumberGenerator) and land on a DRAFT application (D-26). Company and
// user come from the JWT only (CodingRules 8.1). The survey answers travel with the create
// - the gate is server side, the modal cannot bypass it (D-08).
public record CreateApplicationCommand(
    Guid SchemeId,
    string? ApplicationType,
    bool SurveyReadProcedureManual,
    bool SurveyReadMs1500,
    bool SurveyHandlesProhibited,
    bool SurveyHasIhc) : IRequest<ApplicationResponse>;

// Create response: the rows the acknowledgement step and the application screen header
// (spec 12.5) read straight from - CB Application No. and Halal Coach are null until their
// own screens fill them (HA-03 / HA-07).
public record ApplicationResponse(
    Guid Id,
    string ReferenceNo,
    string ApplicationType,
    string Status,
    DateTime StatusDate,
    Guid SchemeId,
    string SchemeName,
    Guid? BatchId,
    string? BatchName,
    bool SurveyReadProcedureManual,
    bool SurveyReadMs1500,
    bool SurveyHandlesProhibited,
    bool SurveyHasIhc);

public class CreateApplicationValidator : AbstractValidator<CreateApplicationCommand>
{
    public CreateApplicationValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.SchemeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Scheme is required.")
            .MustAsync((id, ct) => db.Schemes.AnyAsync(row => row.Id == id, ct))
            .WithMessage("Scheme not found.");

        RuleFor(x => x.ApplicationType)
            .MaximumLength(20).WithMessage("Application type must be 20 characters or fewer.")
            .Must(value => string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "New", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Renewal", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Application type must be New or Renewal.");
    }
}

public class CreateApplicationHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IReferenceNumberGenerator referenceNumbers)
    : IRequestHandler<CreateApplicationCommand, ApplicationResponse>
{
    public async Task<ApplicationResponse> Handle(
        CreateApplicationCommand request,
        CancellationToken ct)
    {
        var scheme = await db.Schemes.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.SchemeId, ct)
            ?? throw new NotFoundException("Scheme not found.");

        // D-08: only Yes / Yes / No / Yes may proceed. The message text is ours - the spec
        // states the rule but never captured a string (spec 12.4 [VERIFY], D-08 default).
        if (!request.SurveyReadProcedureManual
            || !request.SurveyReadMs1500
            || request.SurveyHandlesProhibited
            || !request.SurveyHasIhc)
        {
            throw new BusinessRuleException(
                "Your answers do not allow you to proceed with a Halal application.");
        }

        // D-09 builds {Prefix}({SchemeCode})/... - a scheme without a code cannot get a
        // reference number. Rejected the same way D-09 rejects a brand without an audit
        // prefix (the message mirrors it; the text is ours).
        if (string.IsNullOrWhiteSpace(scheme.Code))
            throw new BusinessRuleException(
                $"Scheme code is not set for scheme {scheme.Name}");

        var referenceNo = await referenceNumbers.NextApplicationReferenceNumberAsync(
            scheme.Code, DateOnly.FromDateTime(DateTime.UtcNow), ct);

        var now = DateTime.UtcNow;
        var entity = new ApplicationEntity
        {
            CompanyId = user.CompanyId,
            ReferenceNo = referenceNo,
            ApplicationType = NormalizeApplicationType(request.ApplicationType),
            Status = ApplicationStatus.Draft,
            StatusDate = now,
            SchemeId = request.SchemeId,
            SurveyReadProcedureManual = request.SurveyReadProcedureManual,
            SurveyReadMs1500 = request.SurveyReadMs1500,
            SurveyHandlesProhibited = request.SurveyHandlesProhibited,
            SurveyHasIhc = request.SurveyHasIhc
        };
        db.Applications.Add(entity);

        // D-26: every status change writes AppApplicationStatusHistories; creation is the
        // first one - no FromStatus yet, the company gets its DRAFT.
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistoryEntity
        {
            CompanyId = user.CompanyId,
            ApplicationId = entity.Id,
            FromStatus = null,
            ToStatus = ApplicationStatus.Draft,
            ChangedAt = now,
            ChangedBy = Guid.TryParse(user.UserId, out var changedBy) ? changedBy : null
        });

        await db.SaveChangesAsync(ct);

        return new ApplicationResponse(
            entity.Id,
            entity.ReferenceNo,
            entity.ApplicationType,
            entity.Status,
            entity.StatusDate,
            entity.SchemeId,
            scheme.Name,
            entity.BatchId,
            null, // BatchName: no batch is chosen at creation (spec 12.3).
            entity.SurveyReadProcedureManual,
            entity.SurveyReadMs1500,
            entity.SurveyHandlesProhibited,
            entity.SurveyHasIhc);
    }

    // Canonical Database.md 10 casing ("New", "Renewal") whatever casing the picker sent.
    private static string NormalizeApplicationType(string? applicationType) =>
        string.IsNullOrWhiteSpace(applicationType)
            ? "New"
            : string.Equals(applicationType, "Renewal", StringComparison.OrdinalIgnoreCase)
                ? "Renewal"
                : "New";
}
