using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Detail of the "Manage External Audit Report and Non Conformance" header form (spec
// 14.10): the stored header fields plus the premise picker's current selection. The NC
// and Attachment tabs load through their own endpoints. Unknown or foreign reports are
// both 404 (house rule). Behind Audit.ExternalReport View.
public record GetExternalAuditReportByIdQuery(Guid Id)
    : IRequest<ExternalAuditReportDetailResponse>;

public record ExternalAuditReportDetailResponse(
    Guid Id,
    string? AuditReferenceNo,
    Guid ReceivedFromCertificationBodyId,
    string ReceivedFrom,
    string CbAuditReferenceNo,
    string TypeOfApplication,
    string TypeOfInspection,
    Guid ApplicationSchemeId,
    string ApplicationScheme,
    string? CbHalalApplicationNo,
    string? CertificationNo,
    DateOnly AuditDate,
    TimeSpan TimeIn,
    TimeSpan TimeOut,
    string? Remarks,
    string? OverallStatus,
    string? Status,
    IReadOnlyList<ExternalReportPremiseOption> Premises);

public class GetExternalAuditReportByIdValidator : AbstractValidator<GetExternalAuditReportByIdQuery>
{
    public GetExternalAuditReportByIdValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetExternalAuditReportByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetExternalAuditReportByIdQuery, ExternalAuditReportDetailResponse>
{
    public async Task<ExternalAuditReportDetailResponse> Handle(
        GetExternalAuditReportByIdQuery request,
        CancellationToken ct)
    {
        var report = await ExternalReportQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("External audit report not found.");

        return await ExternalReportQueries.ToDetailAsync(db, user, report, ct);
    }
}

// Shared load + projection of the External Report features.
internal static class ExternalReportQueries
{
    public static IQueryable<ExternalAuditReportEntity> Own(VHSmartDbContext db, ICurrentUser user) =>
        db.ExternalAuditReports.Where(row => row.CompanyId == user.CompanyId);

    public static async Task<ExternalAuditReportDetailResponse> ToDetailAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        ExternalAuditReportEntity report,
        CancellationToken ct)
    {
        var receivedFrom = await db.CertificationBodies.AsNoTracking()
            .Where(row => row.Id == report.ReceivedFromCertificationBodyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var schemeName = await db.Schemes.AsNoTracking()
            .Where(row => row.Id == report.ApplicationSchemeId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var premises = await (
                from link in db.ExternalAuditReportPremises.AsNoTracking()
                where link.ExternalAuditReportId == report.Id
                select new { link.PremiseId })
            .ToListAsync(ct);
        var premiseOptions = new List<ExternalReportPremiseOption>();
        foreach (var link in premises)
        {
            var option = await db.Premises.AsNoTracking()
                .Where(row => row.Id == link.PremiseId)
                .Select(row => new ExternalReportPremiseOption(row.Id, row.Name, row.StoreCode))
                .FirstOrDefaultAsync(ct);
            if (option != null)
            {
                premiseOptions.Add(option);
            }
        }

        return new ExternalAuditReportDetailResponse(
            report.Id,
            report.AuditReferenceNo,
            report.ReceivedFromCertificationBodyId,
            receivedFrom,
            report.CbAuditReferenceNo,
            report.TypeOfApplication,
            report.TypeOfInspection,
            report.ApplicationSchemeId,
            schemeName,
            report.CbHalalApplicationNo,
            report.CertificationNo,
            DateOnly.FromDateTime(report.AuditDate),
            report.TimeIn,
            report.TimeOut,
            report.Remarks,
            report.OverallStatus,
            report.Status,
            [.. premiseOptions.OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)]);
    }
}
