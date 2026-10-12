using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Add one NC on the report's tab (spec 14.10 "add one NC per press of Add", form fields
// Item / Details / Reference / Clause / Non Conformance Details / Category* / Remarks*).
// Item and Details are the premise/manufacturer and menu/product picker values stored as
// text (Database.md 13 has no FK for them beyond the optional PremiseId; the picker data
// source is [VERIFY], so PremiseId stays null (flagged)). Category* must be one of the
// caller's own General Data "Non Compliance Category" values (Database.md 13), Remarks*
// is required per the form. Source = External, Status = Open (D-05), IsFinal = false.
// AuditReferenceNo copies the report's value (currently always null - flagged).
public record CreateNonConformanceCommand(
    Guid Id,
    string? ItemName,
    string? DetailName,
    string? Reference,
    string? Clause,
    string? NonConformanceDetails,
    Guid CategoryId,
    string Remarks) : IRequest<NonConformanceResponse>;

public class CreateNonConformanceValidator : AbstractValidator<CreateNonConformanceCommand>
{
    public CreateNonConformanceValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.ItemName)
            .MaximumLength(500).WithMessage("Item cannot exceed 500 characters.");

        RuleFor(x => x.DetailName)
            .MaximumLength(500).WithMessage("Details cannot exceed 500 characters.");

        RuleFor(x => x.Reference)
            .MaximumLength(200).WithMessage("Reference cannot exceed 200 characters.");

        RuleFor(x => x.Clause)
            .MaximumLength(200).WithMessage("Clause cannot exceed 200 characters.");

        RuleFor(x => x.NonConformanceDetails)
            .MaximumLength(2000)
            .WithMessage("Non Conformance Details cannot exceed 2000 characters.");

        RuleFor(x => x.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == ExternalReportReferenceData.NonComplianceCategory,
                ct))
            .WithMessage("Category not found.");

        RuleFor(x => x.Remarks)
            .NotEmpty().WithMessage("Remarks is required.")
            .MaximumLength(1000).WithMessage("Remarks cannot exceed 1000 characters.");
    }
}

public class CreateNonConformanceHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateNonConformanceCommand, NonConformanceResponse>
{
    public async Task<NonConformanceResponse> Handle(
        CreateNonConformanceCommand request,
        CancellationToken ct)
    {
        var report = await ExternalReportQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("External audit report not found.");

        var nonConformance = new NonConformanceEntity
        {
            CompanyId = user.CompanyId,
            Source = NonConformanceSource.External,
            AuditReferenceNo = report.AuditReferenceNo,
            ExternalAuditReportId = report.Id,
            PremiseId = null,
            ItemName = request.ItemName,
            DetailName = request.DetailName,
            Reference = request.Reference,
            Clause = request.Clause,
            Findings = null,
            NonConformanceDetails = request.NonConformanceDetails,
            CategoryId = request.CategoryId,
            Remarks = request.Remarks,
            Tags = null,
            Status = NonConformanceStatus.Open,
            IsFinal = false
        };
        db.NonConformances.Add(nonConformance);
        await db.SaveChangesAsync(ct);

        var categoryName = await db.GeneralData.AsNoTracking()
            .Where(row => row.Id == nonConformance.CategoryId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct);

        return new NonConformanceResponse(
            nonConformance.Id,
            nonConformance.ItemName,
            nonConformance.DetailName,
            nonConformance.Reference,
            nonConformance.Clause,
            nonConformance.NonConformanceDetails,
            nonConformance.Remarks,
            nonConformance.Tags,
            nonConformance.Status,
            categoryName,
            null);
    }
}
