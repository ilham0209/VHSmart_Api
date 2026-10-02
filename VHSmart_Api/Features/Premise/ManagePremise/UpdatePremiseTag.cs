using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// "Update Premise Tag" (spec 7.7 [CODE], the list's Tag column): sets the premise's TagId to
// a value of the caller's own COMPANY / "Premise Tag" General Data (Database.md section on
// AdmGeneralData; only editable through Admin General Data, same reasoning as Brand). The
// column shows the tag's name, or the "CLICK TO ADD" placeholder when no tag is set
// (spec 7.7) - the client renders null as that placeholder. This task sets the tag only:
// the spec shows no way to clear it, and no removal is documented.
public record UpdatePremiseTagCommand(Guid Id, Guid? TagId) : IRequest;

public class UpdatePremiseTagValidator : AbstractValidator<UpdatePremiseTagCommand>
{
    public UpdatePremiseTagValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();
        // Own company, Premise Tag category only: a foreign or unknown id is a 400, never a
        // silently accepted dangling reference.
        RuleFor(x => x.TagId)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Premise tag is required.")
            .MustAsync((tagId, ct) => db.GeneralData.AnyAsync(
                row => row.Id == tagId
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.COMPANY
                    && row.Category == "Premise Tag", ct))
            .WithMessage("Premise tag not found.");
    }
}

public class UpdatePremiseTagHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdatePremiseTagCommand>
{
    public async Task Handle(UpdatePremiseTagCommand request, CancellationToken ct)
    {
        var entity = await db.Premises.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Premise not found.");

        entity.TagId = request.TagId;
        await db.SaveChangesAsync(ct);
    }
}
