using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// "Multiple Delete" of the spec 10.2 toolbar (spec 1.1: many lists carry it, [CONFIRMED] for
// this screen): one request, one soft delete for every selected row. The batch is all-or-nothing
// - an id that is unknown, foreign or shared-with-the-caller answers 404 before anything is
// deleted, so a stale selection never half-applies (no row may silently survive a delete the
// user asked for).
public record BulkDeleteRawMaterialsCommand(IReadOnlyList<Guid>? Ids) : IRequest;

public class BulkDeleteRawMaterialsValidator : AbstractValidator<BulkDeleteRawMaterialsCommand>
{
    public BulkDeleteRawMaterialsValidator()
    {
        RuleFor(x => x.Ids)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("No rows selected.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("No rows selected.");
    }
}

public class BulkDeleteRawMaterialsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<BulkDeleteRawMaterialsCommand>
{
    public async Task Handle(BulkDeleteRawMaterialsCommand request, CancellationToken ct)
    {
        var requestedIds = request.Ids!.Distinct().ToList();
        var entities = await db.RawMaterials
            .Where(row => requestedIds.Contains(row.Id))
            .ToListAsync(ct);

        // Missing ids are reported one by one in the same order the caller sent them, so the
        // message points at the row that actually failed.
        foreach (var id in requestedIds)
        {
            var entity = entities.FirstOrDefault(row => row.Id == id);
            if (entity is null)
                throw new NotFoundException("Raw material not found.");

            RawMaterialData.EnsureOwner(entity, user);
        }

        db.RawMaterials.RemoveRange(entities);
        await db.SaveChangesAsync(ct);
    }
}
