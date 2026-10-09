using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditPrefix;

// Edit a prefix row (spec 14.2 pencil action): the same fields as the add modal. Unknown or
// foreign row -> 404; the duplicate rule (Database.md 14.2 UQ (CompanyId, BrandId)) excludes
// the row itself, so keeping its own brand is not a conflict, and moving to a brand whose
// slot is taken is 409 with the same friendly message as create. CompanyId and therefore
// the row's company are never editable (spec 3.3, CodingRules 8.1).
public record UpdateAuditPrefixCommand(
    Guid Id,
    Guid BrandId,
    string Prefix,
    string? Description) : IRequest<AuditPrefixResponse>;

public class UpdateAuditPrefixValidator : AbstractValidator<UpdateAuditPrefixCommand>
{
    public UpdateAuditPrefixValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

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

public class UpdateAuditPrefixHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateAuditPrefixCommand, AuditPrefixResponse>
{
    public async Task<AuditPrefixResponse> Handle(
        UpdateAuditPrefixCommand request,
        CancellationToken ct)
    {
        var entity = await db.AuditPrefixes
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Audit prefix not found.");

        var duplicate = await db.AuditPrefixes.AnyAsync(
            row => row.Id != entity.Id
                && row.CompanyId == user.CompanyId
                && row.BrandId == request.BrandId,
            ct);
        if (duplicate)
            throw new ConflictException("An audit prefix already exists for this brand.");

        entity.BrandId = request.BrandId;
        entity.Prefix = request.Prefix;
        entity.Description = request.Description;
        await db.SaveChangesAsync(ct);

        return new AuditPrefixResponse(
            entity.Id, entity.BrandId, entity.Prefix, entity.Description);
    }
}
