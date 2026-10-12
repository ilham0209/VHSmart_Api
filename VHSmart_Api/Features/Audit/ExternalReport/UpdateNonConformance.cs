using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Edit one NC of the report's tab (the grid Action column - view/edit semantics are
// [VERIFY], edit is included and no status gate is invented (flagged); the D-05 lifecycle
// moves through the corrective-action flow of AU-10, not here). Same field rules as
// create; Source / Status / IsFinal / report link are never touched.
public record UpdateNonConformanceCommand(
    Guid Id,
    string? ItemName,
    string? DetailName,
    string? Reference,
    string? Clause,
    string? NonConformanceDetails,
    Guid CategoryId,
    string Remarks) : IRequest<NonConformanceResponse>;

public class UpdateNonConformanceValidator : AbstractValidator<UpdateNonConformanceCommand>
{
    public UpdateNonConformanceValidator(VHSmartDbContext db, ICurrentUser user)
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

public class UpdateNonConformanceHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateNonConformanceCommand, NonConformanceResponse>
{
    public async Task<NonConformanceResponse> Handle(
        UpdateNonConformanceCommand request,
        CancellationToken ct)
    {
        var nonConformance = await db.NonConformances
            .FirstOrDefaultAsync(row =>
                row.Id == request.Id
                && row.CompanyId == user.CompanyId
                && row.ExternalAuditReportId != null, ct)
            ?? throw new NotFoundException("Non conformance not found.");

        // The owning report must still be live and own-company (a deleted report's NCs
        // are soft-deleted with it, but the guard keeps foreign ids out).
        var reportOwned = await ExternalReportQueries.Own(db, user)
            .AsNoTracking()
            .AnyAsync(row => row.Id == nonConformance.ExternalAuditReportId, ct);
        if (!reportOwned)
        {
            throw new NotFoundException("Non conformance not found.");
        }

        nonConformance.ItemName = request.ItemName;
        nonConformance.DetailName = request.DetailName;
        nonConformance.Reference = request.Reference;
        nonConformance.Clause = request.Clause;
        nonConformance.NonConformanceDetails = request.NonConformanceDetails;
        nonConformance.CategoryId = request.CategoryId;
        nonConformance.Remarks = request.Remarks;

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
