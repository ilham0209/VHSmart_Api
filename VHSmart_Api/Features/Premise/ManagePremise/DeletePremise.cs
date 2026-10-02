using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Delete action of the Premise list (spec 7.7, Action column: view, edit, delete): soft
// delete (CodingRules 7.1). The Contact Person and Hostel rows stay with the premise, like
// the staff attachments P-01 left behind - their only list is the premise detail, which
// disappears with the parent, and the filtered UQ (CompanyId, Email) / (CompanyId,
// StoreCode) free both values for reuse. The row must belong to the caller's own company;
// everything else answers 404 (never 403). The legacy "Premise deleted" notification
// (spec 7.7 [CODE] 21.8) is NOT sent: the spec gives no recipient rule for it - flagged in
// the report. Premise entitlement caps (ADC <= 5, LTE <= 1) are PK-01 scope, not checked
// here (delete only frees a slot).
public record DeletePremiseCommand(Guid Id) : IRequest;

public class DeletePremiseHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeletePremiseCommand>
{
    public async Task Handle(DeletePremiseCommand request, CancellationToken ct)
    {
        var entity = await db.Premises.FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Premise not found.");

        db.Premises.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
