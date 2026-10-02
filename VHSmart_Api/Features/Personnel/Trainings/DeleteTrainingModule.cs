using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Trainings;

// Delete action of the Training Module table (spec 7.5 "Training Module List" Action column):
// soft delete (CodingRules 7.1) - the document bytes stay with the row, exactly like the
// staff attachment and the R-05 icon. The row must belong to the training in the route AND to
// the caller's own company; everything else answers 404.
public record DeleteTrainingModuleCommand(Guid TrainingId, Guid ModuleId) : IRequest;

public class DeleteTrainingModuleHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteTrainingModuleCommand>
{
    public async Task Handle(DeleteTrainingModuleCommand request, CancellationToken ct)
    {
        var entity = await db.TrainingModules.FirstOrDefaultAsync(
                row => row.Id == request.ModuleId
                    && row.TrainingId == request.TrainingId
                    && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Training module not found.");

        db.TrainingModules.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
