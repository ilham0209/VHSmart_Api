using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// Create a batch (spec 12.2 add modal): Scheme*, Batch Name*, CB Reference No., Submission
// Planned Date, For Company* (from the JWT), Description; BrandId is required by the legacy
// rule "Brand Owner is required in every case". ManufacturerSupplierId and the product /
// premise links are set on edit (the add modal has none of them). Duplicate Name within the
// company -> 409 with the legacy message verbatim "Error! Please provide unique batch name".
public record CreateBatchCommand(
    Guid SchemeId,
    string Name,
    string? CbReferenceNo,
    DateTime? SubmissionPlannedDate,
    Guid? BrandId,
    string? Description) : IRequest<BatchResponse>;

public class CreateBatchValidator : AbstractValidator<CreateBatchCommand>
{
    public CreateBatchValidator(VHSmartDbContext db, ICurrentUser user)
    {
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

        RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Description must be 1000 characters or fewer.");
    }
}

public class CreateBatchHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateBatchCommand, BatchResponse>
{
    public async Task<BatchResponse> Handle(
        CreateBatchCommand request,
        CancellationToken ct)
    {
        var duplicate = await db.Batches.AnyAsync(
            row => row.CompanyId == user.CompanyId && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("Error! Please provide unique batch name");

        var entity = new BatchEntity
        {
            CompanyId = user.CompanyId
        };
        BatchData.Apply(
            entity, request.SchemeId, request.Name, request.CbReferenceNo,
            request.SubmissionPlannedDate, request.BrandId, manufacturerSupplierId: null,
            request.Description);
        db.Batches.Add(entity);
        await db.SaveChangesAsync(ct);

        return await BatchResponseData.From(db, entity, ct);
    }
}
