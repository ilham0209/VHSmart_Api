using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// "Associate Premise" of the Food Premise batch edit modal (spec 12.2 flow 6): one premise
// joins one batch. AppBatchPremises has no MappingStatus (Database.md 10) - it is a plain
// association, so a second live row for the pair cannot exist (the filtered unique index)
// and re-linking an already linked premise just answers the row it has. Three gates before
// the write: the batch must be the caller's own row and a Food Premise batch (Database.md
// 10), the premise must be the caller's own row, and D-15 must find it COMPLETE - only
// premises with complete documentation are selectable (the Batch > Associate Premise rule;
// the spec line that says so is [VERIFY] and D-15 is its default).
public record LinkBatchPremiseCommand(Guid BatchId, Guid PremiseId)
    : IRequest<BatchPremiseResponse>;

public class LinkBatchPremiseValidator : AbstractValidator<LinkBatchPremiseCommand>
{
    public LinkBatchPremiseValidator()
    {
        RuleFor(x => x.PremiseId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Premise is required.");
    }
}

public class LinkBatchPremiseHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<LinkBatchPremiseCommand, BatchPremiseResponse>
{
    public async Task<BatchPremiseResponse> Handle(
        LinkBatchPremiseCommand request,
        CancellationToken ct)
    {
        var batch = await db.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.BatchId && row.CompanyId == user.CompanyId, ct);
        if (batch is null)
            throw new NotFoundException("Batch not found.");

        if (!await BatchData.IsFoodPremiseSchemeAsync(db, batch.SchemeId, ct))
            throw new BusinessRuleException("This batch does not accept premises.");

        var premise = await db.Premises
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId, ct);
        if (premise is null)
            throw new NotFoundException("Premise not found.");

        // D-15: the pick-list offers only complete premises; a direct call over the API is
        // held to the same rule instead of trusting the client to have filtered.
        var completeIds = await BatchData.LoadCompletePremiseIdsAsync(
            db, [request.PremiseId], ct);
        if (!completeIds.Contains(request.PremiseId))
            throw new BusinessRuleException(
                "Only premises with complete documentation can be added to a batch.");

        var entity = await db.BatchPremises.FirstOrDefaultAsync(
            row => row.BatchId == request.BatchId && row.PremiseId == request.PremiseId,
            ct);

        if (entity is null)
        {
            entity = new BatchPremiseEntity
            {
                // The link belongs to the company that owns the batch (JWT only).
                CompanyId = user.CompanyId,
                BatchId = request.BatchId,
                PremiseId = request.PremiseId
            };
            db.BatchPremises.Add(entity);
            await db.SaveChangesAsync(ct);
        }

        return new BatchPremiseResponse(entity.Id, entity.PremiseId, premise.Name);
    }
}
