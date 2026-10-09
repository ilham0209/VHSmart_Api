using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// Edit a batch (spec 12.2 edit modal): the same fields as Create plus Manufacturer*, which
// the spec adds on edit ("Edit modal adds Manufacturer*") and Database.md 10 requires for
// every scheme that is not Food Premise ("other schemes -> products + manufacturer"; a Food
// Premise batch picks its brand instead - spec 12.2 flow 6). The product / premise links are
// separate link-unlink endpoints, exactly like the ingredient tab of Manage Product. Unknown
// or foreign batch -> 404, and the duplicate-name rule excludes the row itself so keeping its
// own name is not a conflict (the manufacturer e-mail stance of RM-01).
public record UpdateBatchCommand(
    Guid Id,
    Guid SchemeId,
    string Name,
    string? CbReferenceNo,
    DateTime? SubmissionPlannedDate,
    Guid? BrandId,
    Guid? ManufacturerSupplierId,
    string? Description) : IRequest<BatchResponse>;

public class UpdateBatchValidator : AbstractValidator<UpdateBatchCommand>
{
    public UpdateBatchValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.SchemeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Scheme is required.")
            .MustAsync((id, ct) => db.Schemes.AnyAsync(row => row.Id == id, ct))
            .WithMessage("Scheme not found.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Batch name is required.")
            .MaximumLength(200).WithMessage("Batch name must be 200 characters or fewer.");

        RuleFor(x => x.CbReferenceNo).MaximumLength(100)
            .WithMessage("CB reference number must be 100 characters or fewer.");

        RuleFor(x => x.BrandId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Brand owner is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.COMPANY
                    && row.Category == "Brand",
                ct))
            .WithMessage("Brand owner not found.");

        // Manufacturer* belongs to the edit modal of a product scheme (spec 12.2); a Food
        // Premise batch never shows the field (flow 6 names brand + company only).
        RuleFor(x => x.ManufacturerSupplierId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (command, id, ct) => id is not null
                || await BatchData.IsFoodPremiseSchemeAsync(db, command.SchemeId, ct))
            .WithMessage("Manufacturer is required.")
            .MustAsync(async (command, id, ct) => id is null
                || await db.ManufacturerSuppliers.AnyAsync(
                    row => row.Id == id
                        && row.CompanyId == user.CompanyId
                        && row.ManufacturerName != null,
                    ct))
            .WithMessage("Manufacturer not found.");

        RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Description must be 1000 characters or fewer.");
    }
}

public class UpdateBatchHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateBatchCommand, BatchResponse>
{
    public async Task<BatchResponse> Handle(
        UpdateBatchCommand request,
        CancellationToken ct)
    {
        var entity = await db.Batches
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Batch not found.");

        var duplicate = await db.Batches.AnyAsync(
            row => row.CompanyId == user.CompanyId
                && row.Name == request.Name
                && row.Id != request.Id,
            ct);
        if (duplicate)
            throw new ConflictException("Error! Please provide unique batch name");

        BatchData.Apply(
            entity, request.SchemeId, request.Name, request.CbReferenceNo,
            request.SubmissionPlannedDate, request.BrandId, request.ManufacturerSupplierId,
            request.Description);
        await db.SaveChangesAsync(ct);

        return await BatchResponseData.From(db, entity, ct);
    }
}
