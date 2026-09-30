using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// Remove a CB (spec 5.2 list: delete action): soft delete (CodingRules 7.1). The spec's
// "delete only when unused" cannot be checked yet - the tables that can reference a CB
// (ComCompanies.CertificationBodyId, AudExternalAuditReports) arrive with C-01 / AU-10; the
// guard belongs to those tasks. Platform-admin only (D-07).
public record DeleteCertificationBodyCommand(Guid Id) : IRequest;

public class DeleteCertificationBodyHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteCertificationBodyCommand>
{
    public async Task Handle(DeleteCertificationBodyCommand request, CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        var entity = await db.CertificationBodies
            .FirstOrDefaultAsync(cb => cb.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Certification body not found.");

        db.CertificationBodies.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
