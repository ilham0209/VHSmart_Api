using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// Remove a concept (spec 9.3 list, Action > delete): soft delete (CodingRules 7.1). The
// "List of Menu" rows stay with the concept, like every other child list in this codebase
// (PD-04's DeleteMenu). There is no "delete only when unused" guard: ComPremises.MenuConceptId
// references this table (PR-04 reads it), and the stance of RM-02 / PD-01 / PD-03 is that the
// guard lands with the task that owns the referencing SCREEN, not here - a soft-deleted
// concept keeps every premise row intact and simply stops answering GET.
// The tenant filter makes an unknown or foreign concept answer 404.
public record DeleteMenuConceptCommand(Guid Id) : IRequest;

public class DeleteMenuConceptHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteMenuConceptCommand>
{
    public async Task Handle(DeleteMenuConceptCommand request, CancellationToken ct)
    {
        var entity = await db.MenuConcepts
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Menu concept not found.");

        db.MenuConcepts.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
