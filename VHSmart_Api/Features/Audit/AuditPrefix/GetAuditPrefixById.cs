using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Audit.AuditPrefix;

// The view/edit modal of one prefix row (spec 14.2 row actions): BrandId preselects the
// dropdown, Brand is the joined name for display. An unknown or foreign (or soft-deleted)
// row answers 404 - the client must not learn that the id exists elsewhere (CodingRules 9).
public record GetAuditPrefixByIdQuery(Guid Id) : IRequest<GetAuditPrefixByIdResponse>;

public record GetAuditPrefixByIdResponse(
    Guid Id,
    Guid BrandId,
    string Brand,
    string AuditPrefix,
    string? Description,
    DateTime? ModifiedDate);

public class GetAuditPrefixByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetAuditPrefixByIdQuery, GetAuditPrefixByIdResponse>
{
    public async Task<GetAuditPrefixByIdResponse> Handle(
        GetAuditPrefixByIdQuery request,
        CancellationToken ct)
    {
        var row = await (
                from prefix in db.AuditPrefixes.AsNoTracking()
                where prefix.Id == request.Id
                select new GetAuditPrefixByIdResponse(
                    prefix.Id,
                    prefix.BrandId,
                    (from brand in db.GeneralData
                     where brand.Id == prefix.BrandId
                     select brand.Name).FirstOrDefault() ?? string.Empty,
                    prefix.Prefix,
                    prefix.Description,
                    prefix.SysDateModified))
            .FirstOrDefaultAsync(ct);

        if (row is null)
            throw new NotFoundException("Audit prefix not found.");

        return row;
    }
}
