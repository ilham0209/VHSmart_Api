using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.HalalPolicy;

// Delete action of the Halal Policy list (spec 7.2): soft delete (CodingRules 7.1) - the
// document bytes stay with the row, exactly like the Web Link icon, so a soft-deleted row
// keeps a consistent File column group. The unique (CompanyId, SchemeId) index is filtered
// on IsDeleted, so deleting frees the scheme for a re-upload. The explicit CompanyId match
// keeps a ViewAllCompanies caller on their own rows; everything else answers 404.
public record DeleteHalalPolicyCommand(Guid Id) : IRequest;

public class DeleteHalalPolicyHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteHalalPolicyCommand>
{
    public async Task Handle(DeleteHalalPolicyCommand request, CancellationToken ct)
    {
        var entity = await db.HalalPolicies
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Halal policy not found.");

        db.HalalPolicies.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
