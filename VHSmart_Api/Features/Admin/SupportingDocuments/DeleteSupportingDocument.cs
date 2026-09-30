using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// Remove a Supporting Document (spec 5.5 list: delete action): soft delete (CodingRules 7.1) -
// the template bytes stay with the row, exactly like the CB logo and the web-link icon, so a
// soft-deleted row keeps a consistent File column group. The tenant filter makes another
// company's row (or an already deleted one) answer 404 instead of 204 (CodingRules 9).
public record DeleteSupportingDocumentCommand(Guid Id) : IRequest;

public class DeleteSupportingDocumentHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteSupportingDocumentCommand>
{
    public async Task Handle(DeleteSupportingDocumentCommand request, CancellationToken ct)
    {
        var entity = await db.SupportingDocuments
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Supporting document not found.");

        db.SupportingDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
