using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Header form save (spec 14.10 [CONFIRMED]): the CB, the typed CB reference, the D-10
// dropdown values stored as chosen texts, the scheme, the optional CB application /
// certification numbers, Audit Date + Time In + Time Out (typed by the caller; no
// ordering rule is documented for the external report - D-14 covers meetings only, so
// none is invented (flagged)), Overall Status from the D-10 fixed list and the "List of
// Premise" multi-select (own live premises, non-empty - the spec marks For Company* and
// the premise list as required). Status is stamped Opened (spec 14.12, the only defined
// value). AuditReferenceNo stays null: the legacy "EXT-SUP/..." format is [VERIFY] with
// no Decisions.md default (flagged). No notification (D-25).
public record CreateExternalAuditReportCommand(
    Guid ReceivedFromCertificationBodyId,
    string CbAuditReferenceNo,
    string TypeOfApplication,
    string TypeOfInspection,
    Guid ApplicationSchemeId,
    string? CbHalalApplicationNo,
    string? CertificationNo,
    DateOnly AuditDate,
    TimeSpan TimeIn,
    TimeSpan TimeOut,
    string? Remarks,
    string OverallStatus,
    IReadOnlyList<Guid> PremiseIds) : IRequest<ExternalAuditReportDetailResponse>;

public class CreateExternalAuditReportValidator
    : AbstractValidator<CreateExternalAuditReportCommand>
{
    public CreateExternalAuditReportValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.ReceivedFromCertificationBodyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Received From is required.")
            .MustAsync((id, ct) => db.CertificationBodies.AnyAsync(
                row => row.Id == id, ct))
            .WithMessage("Received From not found.");

        RuleFor(x => x.CbAuditReferenceNo)
            .NotEmpty().WithMessage("CB Audit Reference No is required.")
            .MaximumLength(120).WithMessage("CB Audit Reference No cannot exceed 120 characters.");

        RuleFor(x => x.TypeOfApplication)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Type of Application is required.")
            .MustAsync((value, ct) => IsGeneralDataValueAsync(
                db, user.CompanyId,
                ExternalReportReferenceData.TypeOfApplicationCategory, value, ct))
            .WithMessage("Type of Application not found.");

        RuleFor(x => x.TypeOfInspection)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Type of Inspection is required.")
            .MustAsync((value, ct) => IsGeneralDataValueAsync(
                db, user.CompanyId,
                ExternalReportReferenceData.TypeOfInspectionCategory, value, ct))
            .WithMessage("Type of Inspection not found.");

        RuleFor(x => x.ApplicationSchemeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Application Scheme is required.")
            .MustAsync((id, ct) => db.Schemes.AnyAsync(row => row.Id == id, ct))
            .WithMessage("Application Scheme not found.");

        RuleFor(x => x.CbHalalApplicationNo)
            .MaximumLength(100).WithMessage("CB Halal Application No cannot exceed 100 characters.");

        RuleFor(x => x.CertificationNo)
            .MaximumLength(100).WithMessage("Certification No cannot exceed 100 characters.");

        RuleFor(x => x.AuditDate)
            .Must(value => value != default)
            .WithMessage("Audit Date is required.");

        RuleFor(x => x.TimeIn)
            .Must(value => value != default)
            .WithMessage("Time In is required.");

        RuleFor(x => x.TimeOut)
            .Must(value => value != default)
            .WithMessage("Time Out is required.");

        RuleFor(x => x.Remarks)
            .MaximumLength(1000).WithMessage("Remarks cannot exceed 1000 characters.");

        RuleFor(x => x.OverallStatus)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Overall Status is required.")
            .Must(value => ExternalReportReferenceData.OverallStatuses.Contains(value))
            .WithMessage("Overall Status must be Pass, Fail or Pending.");

        RuleFor(x => x.PremiseIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Premise is required.")
            .MustAsync(async (premiseIds, ct) => await db.Premises.CountAsync(
                row => row.CompanyId == user.CompanyId
                    && premiseIds.Contains(row.Id), ct) == premiseIds.Distinct().Count())
            .WithMessage("Premise not found.");

        static async Task<bool> IsGeneralDataValueAsync(
            VHSmartDbContext db,
            Guid companyId,
            string category,
            string value,
            CancellationToken ct) =>
            await db.GeneralData.AnyAsync(
                row => row.CompanyId == companyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == category
                    && row.Name == value,
                ct);
    }
}

public class CreateExternalAuditReportHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateExternalAuditReportCommand, ExternalAuditReportDetailResponse>
{
    public async Task<ExternalAuditReportDetailResponse> Handle(
        CreateExternalAuditReportCommand request,
        CancellationToken ct)
    {
        var report = new ExternalAuditReportEntity
        {
            CompanyId = user.CompanyId,
            // EXT reference format is [VERIFY] with no default - left null (flagged).
            AuditReferenceNo = null,
            ReceivedFromCertificationBodyId = request.ReceivedFromCertificationBodyId,
            CbAuditReferenceNo = request.CbAuditReferenceNo,
            TypeOfApplication = request.TypeOfApplication,
            TypeOfInspection = request.TypeOfInspection,
            ApplicationSchemeId = request.ApplicationSchemeId,
            CbHalalApplicationNo = request.CbHalalApplicationNo,
            CertificationNo = request.CertificationNo,
            AuditDate = request.AuditDate.ToDateTime(TimeOnly.MinValue),
            TimeIn = request.TimeIn,
            TimeOut = request.TimeOut,
            Remarks = request.Remarks,
            OverallStatus = request.OverallStatus,
            Status = ExternalAuditReportStatus.Opened
        };
        db.ExternalAuditReports.Add(report);

        foreach (var premiseId in request.PremiseIds.Distinct())
        {
            db.ExternalAuditReportPremises.Add(new ExternalAuditReportPremiseEntity
            {
                CompanyId = user.CompanyId,
                ExternalAuditReportId = report.Id,
                PremiseId = premiseId
            });
        }

        await db.SaveChangesAsync(ct);

        return await ExternalReportQueries.ToDetailAsync(db, user, report, ct);
    }
}
