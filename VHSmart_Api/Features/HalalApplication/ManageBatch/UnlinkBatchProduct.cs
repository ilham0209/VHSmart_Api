using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The Unlink half of the edit modal's product Action icon (spec 12.2): Database.md 10 says
// link/unlink TOGGLES MappingStatus, so the row is kept and marked INACTIVE - never a
// physical delete, so the pair stays on the table showing its status and a later link just
// flips it back (the UnlinkProductIngredient stance). Re-unlinking an already INACTIVE row
// is a no-op that still answers 204. The batch id is scoped in the route: a row of another
// batch answers 404 (never 403), and the batch must belong to the caller's company.
public record UnlinkBatchProductCommand(Guid BatchId, Guid BatchProductId) : IRequest;

public class UnlinkBatchProductValidator : AbstractValidator<UnlinkBatchProductCommand>
{
    public UnlinkBatchProductValidator()
    {
        RuleFor(x => x.BatchProductId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Product link is required.");
    }
}

public class UnlinkBatchProductHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UnlinkBatchProductCommand>
{
    public async Task Handle(UnlinkBatchProductCommand request, CancellationToken ct)
    {
        var batchExists = await db.Batches
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.BatchId && row.CompanyId == user.CompanyId, ct);
        if (!batchExists)
            throw new NotFoundException("Batch not found.");

        var entity = await db.BatchProducts.FirstOrDefaultAsync(
            row => row.Id == request.BatchProductId && row.BatchId == request.BatchId,
            ct);

        if (entity is null)
            throw new NotFoundException("Product link not found.");

        entity.MappingStatus = BatchProductMappingStatus.Inactive;
        await db.SaveChangesAsync(ct);
    }
}
