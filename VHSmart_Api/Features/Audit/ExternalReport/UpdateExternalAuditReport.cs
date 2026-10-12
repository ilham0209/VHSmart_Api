using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Edit of the header form (spec 14.10: the list Action carries edit). Same field rules
// as create (shared via CreateExternalAuditReportValidator's shape); the premise links
// are rebuilt the AU-06 member way - old links soft-deleted, the picked set re-inserted
// (the filtered UQ frees the pairs). Status and the (null) AuditReferenceNo are never
// touched here: the lifecycle beyond Opened is [VERIFY].
public record UpdateExternalAuditReportCommand(
    Guid Id,
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

public class UpdateExternalAuditReportValidator
    : AbstractValidator<UpdateExternalAuditReportCommand>
{
    public UpdateExternalAuditReportValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

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
            .MustAsync((value, ct) => db.GeneralData.AnyAsync(
                row => row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == ExternalReportReferenceData.TypeOfApplicationCategory
                    && row.Name == value,
                ct))
            .WithMessage("Type of Application not found.");

        RuleFor(x => x.TypeOfInspection)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Type of Inspection is required.")
            .MustAsync((value, ct) => db.GeneralData.AnyAsync(
                row => row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == ExternalReportReferenceData.TypeOfInspectionCategory
                    && row.Name == value,
                ct))
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
    }
}

public class UpdateExternalAuditReportHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateExternalAuditReportCommand, ExternalAuditReportDetailResponse>
{
    public async Task<ExternalAuditReportDetailResponse> Handle(
        UpdateExternalAuditReportCommand request,
        CancellationToken ct)
    {
        var report = await ExternalReportQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("External audit report not found.");

        report.ReceivedFromCertificationBodyId = request.ReceivedFromCertificationBodyId;
        report.CbAuditReferenceNo = request.CbAuditReferenceNo;
        report.TypeOfApplication = request.TypeOfApplication;
        report.TypeOfInspection = request.TypeOfInspection;
        report.ApplicationSchemeId = request.ApplicationSchemeId;
        report.CbHalalApplicationNo = request.CbHalalApplicationNo;
        report.CertificationNo = request.CertificationNo;
        report.AuditDate = request.AuditDate.ToDateTime(TimeOnly.MinValue);
        report.TimeIn = request.TimeIn;
        report.TimeOut = request.TimeOut;
        report.Remarks = request.Remarks;
        report.OverallStatus = request.OverallStatus;

        var oldLinks = await db.ExternalAuditReportPremises
            .Where(row => row.ExternalAuditReportId == report.Id)
            .ToListAsync(ct);
        foreach (var oldLink in oldLinks)
        {
            db.ExternalAuditReportPremises.Remove(oldLink);
        }

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
