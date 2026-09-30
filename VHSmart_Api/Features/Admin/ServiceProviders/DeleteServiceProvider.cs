using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.ServiceProviders;

// Remove a Service Provider (spec 5.3 list: delete action): soft delete (CodingRules 7.1).
// The spec's "delete only when unused" cannot be checked yet - PayPayments.ServiceProviderId
// (Database.md, spec 15) arrives with PY-02; the guard belongs to that task, exactly like the
// CB guard was deferred to C-01/AU-10. The tenant filter makes another company's row (or an
// already deleted one) answer 404 instead of 204 (CodingRules 9).
public record DeleteServiceProviderCommand(Guid Id) : IRequest;

public class DeleteServiceProviderHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteServiceProviderCommand>
{
    public async Task Handle(DeleteServiceProviderCommand request, CancellationToken ct)
    {
        var entity = await db.ServiceProviders
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Service provider not found.");

        db.ServiceProviders.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
