using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.GeneralData;

// The view/edit modal of one reference value (spec 5.1 row actions). A row of another company
// or a soft-deleted row is invisible to this context, so both answer 404 - the client must not
// learn that the id exists elsewhere (CodingRules 9).
public record GetGeneralDataByIdQuery(Guid Id) : IRequest<GetGeneralDataByIdResponse>;

public record GetGeneralDataByIdResponse(
    Guid Id,
    GeneralDataGroup Group,
    string Category,
    string Name,
    string? Description,
    DateTime? ModifiedDate);

public class GetGeneralDataByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetGeneralDataByIdQuery, GetGeneralDataByIdResponse>
{
    public async Task<GetGeneralDataByIdResponse> Handle(
        GetGeneralDataByIdQuery request,
        CancellationToken ct)
    {
        var row = await db.GeneralData
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, ct);

        if (row is null)
            throw new NotFoundException("General data not found.");

        return new GetGeneralDataByIdResponse(
            row.Id, row.Group, row.Category, row.Name, row.Description, row.SysDateModified);
    }
}
