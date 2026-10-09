using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The "remove premise-batch mapping" action of the Food Premise batch (spec 12.2 [CODE]
// legacy action). AppBatchPremises carries no MappingStatus (Database.md 10), so unlike a
// product link this IS a soft delete: the row disappears and the filtered unique index
// (BatchId, PremiseId) frees the pair for a re-associate. The batch id is scoped in the
// route - a row of another batch answers 404 (never 403) - and the batch must belong to the
// caller's company.
public record UnlinkBatchPremiseCommand(Guid BatchId, Guid BatchPremiseId) : IRequest;

public class UnlinkBatchPremiseValidator : AbstractValidator<UnlinkBatchPremiseCommand>
{
    public UnlinkBatchPremiseValidator()
    {
        RuleFor(x => x.BatchPremiseId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Premise link is required.");
    }
}

public class UnlinkBatchPremiseHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UnlinkBatchPremiseCommand>
{
    public async Task Handle(UnlinkBatchPremiseCommand request, CancellationToken ct)
    {
        var batchExists = await db.Batches
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.BatchId && row.CompanyId == user.CompanyId, ct);
        if (!batchExists)
            throw new NotFoundException("Batch not found.");

        var entity = await db.BatchPremises.FirstOrDefaultAsync(
            row => row.Id == request.BatchPremiseId && row.BatchId == request.BatchId,
            ct);

        if (entity is null)
            throw new NotFoundException("Premise link not found.");

        db.BatchPremises.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
