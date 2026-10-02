using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Trainings;

// The attendance dropdown of the "Manage Training" modal (spec 7.5: "Attendance* (dropdown of
// staff + '+' to add)"). It repeats the All Staff pick list in a minimal shape (Id, Name,
// Designation) so the Training screen works with Personnel.InternalTraining alone instead of
// requiring the Personnel.AllStaff key (same reasoning as CI-01/CI-03 options endpoints).
// Rows are filtered to the caller's company explicitly - a ViewAll token must not read
// another tenant's staff. The attendance table's IHC columns come from the detail response.
public record GetTrainingStaffOptionsQuery : IRequest<IReadOnlyList<TrainingStaffOptionResponse>>;

public record TrainingStaffOptionResponse(Guid Id, string Name, string Designation);

public class GetTrainingStaffOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetTrainingStaffOptionsQuery, IReadOnlyList<TrainingStaffOptionResponse>>
{
    public async Task<IReadOnlyList<TrainingStaffOptionResponse>> Handle(
        GetTrainingStaffOptionsQuery request,
        CancellationToken ct) =>
        await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new TrainingStaffOptionResponse(
                row.Id,
                row.Name,
                row.Designation == null ? string.Empty : row.Designation.Name))
            .ToListAsync(ct);
}
