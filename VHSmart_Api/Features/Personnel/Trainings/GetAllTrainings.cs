using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Personnel.Trainings;

// Training List (spec 7.5, CONFIRMED columns): Action (client side - only the Id is needed),
// Training Name, Training Type, Company, Training Date. The explicit CompanyId match keeps a
// ViewAllCompanies caller on their own rows (same guard as CI-01/P-01). Search covers the
// name like the other lists; the default order is TrainingDate descending (newest first) -
// the spec does not state a default, the same choice CI-02 made for its date list.
public record GetAllTrainingsQuery : IRequest<DataGridResponse<GetAllTrainingsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllTrainingsResponse(
    Guid Id,
    string Name,
    TrainingType TrainingType,
    string Company,
    DateTime TrainingDate);

public class GetAllTrainingsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllTrainingsQuery, DataGridResponse<GetAllTrainingsResponse>>
{
    public async Task<DataGridResponse<GetAllTrainingsResponse>> Handle(
        GetAllTrainingsQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort ? nameof(GetAllTrainingsResponse.TrainingDate) : request.Request.SortBy;
        var sortDescending = defaultSort || request.Request.SortDescending;

        return await (
                from training in db.Trainings.AsNoTracking()
                    .Where(x => x.CompanyId == user.CompanyId)
                select new GetAllTrainingsResponse(
                    training.Id,
                    training.Name,
                    training.TrainingType,
                    (from company in db.Companies
                     where company.Id == training.CompanyId
                     select company.Name).FirstOrDefault() ?? string.Empty,
                    training.TrainingDate))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllTrainingsResponse.Name))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
