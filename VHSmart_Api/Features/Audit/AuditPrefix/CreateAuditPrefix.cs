using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditPrefix;

// Add a prefix slot (spec 14.2 add modal "Audit Prefix": Audit Prefix*, Brand*, Description).
// Brand is a dropdown of the caller's own COMPANY General Data "Brand" rows - the same set
// GetAuditPrefixOptions serves and CreateBatch accepts - and BrandId comes from that pick;
// CompanyId from the JWT only (CodingRules 8.1). The unique rule (Database.md 14.2 UQ
// (CompanyId, BrandId), "default enforce") means the brand's slot is taken while a live row
// exists: 409 with the friendly message instead of letting the index fire.
public record CreateAuditPrefixCommand(
    Guid BrandId,
    string Prefix,
    string? Description) : IRequest<AuditPrefixResponse>;

// Shared by create and edit - the modal fields are the same (spec 14.2).
public record AuditPrefixResponse(
    Guid Id,
    Guid BrandId,
    string Prefix,
    string? Description);

public class CreateAuditPrefixValidator : AbstractValidator<CreateAuditPrefixCommand>
{
    public CreateAuditPrefixValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.BrandId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Brand is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.COMPANY
                    && row.Category == "Brand",
                ct))
            .WithMessage("Brand not found.");

        RuleFor(x => x.Prefix)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Audit prefix is required.")
            .MaximumLength(20).WithMessage("Audit prefix must be 20 characters or fewer.");

        RuleFor(x => x.Description).MaximumLength(500)
            .WithMessage("Description must be 500 characters or fewer.");
    }
}

public class CreateAuditPrefixHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateAuditPrefixCommand, AuditPrefixResponse>
{
    public async Task<AuditPrefixResponse> Handle(
        CreateAuditPrefixCommand request,
        CancellationToken ct)
    {
        var duplicate = await db.AuditPrefixes.AnyAsync(
            row => row.CompanyId == user.CompanyId && row.BrandId == request.BrandId, ct);
        if (duplicate)
            throw new ConflictException("An audit prefix already exists for this brand.");

        var entity = new AuditPrefixEntity
        {
            CompanyId = user.CompanyId,
            BrandId = request.BrandId,
            Prefix = request.Prefix,
            Description = request.Description
        };
        db.AuditPrefixes.Add(entity);
        await db.SaveChangesAsync(ct);

        return new AuditPrefixResponse(
            entity.Id, entity.BrandId, entity.Prefix, entity.Description);
    }
}
