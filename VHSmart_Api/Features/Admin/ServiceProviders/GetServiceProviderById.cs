using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.ServiceProviders;

// The view/edit form of one Service Provider (spec 5.3 "Manage Services Provider"): every
// form field. The spec's "Company Id" field is display-only - the value comes from the JWT,
// never from the body (CodingRules 8.1), and the tenant filter answers 404 for another
// company's row. Shared by Get / Create / Update like CertificationBodyResponse is.
public record GetServiceProviderByIdQuery(Guid Id) : IRequest<ServiceProviderResponse>;

public record ServiceProviderResponse(
    Guid Id,
    string Name,
    string? Description,
    string Address,
    string Postcode,
    Guid CountryId,
    string State,
    string Telephone,
    string? Fax,
    string? Webpage,
    string Email,
    string ContactPerson,
    string? BankName,
    string? BankAccountNo,
    DateTime? ModifiedDate)
{
    internal static ServiceProviderResponse From(ServiceProviderEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Address,
            entity.Postcode,
            entity.CountryId,
            entity.State,
            entity.Telephone,
            entity.Fax,
            entity.Webpage,
            entity.Email,
            entity.ContactPerson,
            entity.BankName,
            entity.BankAccountNo,
            entity.SysDateModified);
}

public class GetServiceProviderByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetServiceProviderByIdQuery, ServiceProviderResponse>
{
    public async Task<ServiceProviderResponse> Handle(
        GetServiceProviderByIdQuery request,
        CancellationToken ct)
    {
        var entity = await db.ServiceProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Service provider not found.");

        return ServiceProviderResponse.From(entity);
    }
}
