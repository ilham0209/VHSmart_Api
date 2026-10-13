using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.ServiceProviders;

// Remove a Service Provider (spec 5.3 list: delete action): soft delete (CodingRules 7.1).
// "Delete only when unused" (spec 5.3 [MANUAL]): a provider that is the Payee of a live
// Payment > Other row cannot be deleted - the guard belongs to PY-02, which owns
// PayPayments.ServiceProviderId, exactly like the CB guard was deferred to C-01/AU-10; it
// lives here because this handler is the only writer of the row. Soft-deleted payments free
// the provider (the global filter hides them, so only live rows count). The tenant filter
// makes another company's row (or an already deleted one) answer 404 instead of 204
// (CodingRules 9).
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

        var usedByPayment = await db.Payments.OfType<OtherPaymentEntity>()
            .AnyAsync(row => row.ServiceProviderId == entity.Id, ct);
        if (usedByPayment)
        {
            throw new ConflictException(
                "This service provider is used by a payment and cannot be deleted.");
        }

        db.ServiceProviders.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
