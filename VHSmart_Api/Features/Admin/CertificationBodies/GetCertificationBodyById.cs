using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// The view/edit form of one CB (spec 5.2 "Manage Certification Bodies"): every form field
// plus the logo file name (the bytes come from GET {id}/logo). Platform-admin only (D-07).
// Shared by Get / Create / Update like RolePermissionsResponse is shared by the role features.
public record GetCertificationBodyByIdQuery(Guid Id) : IRequest<CertificationBodyResponse>;

public record CertificationBodyResponse(
    Guid Id,
    string Name,
    string? Acronym,
    string? Notes,
    Guid? RepresentingCountryId,
    string? Address1,
    string? Address2,
    string? City,
    string? Postcode,
    Guid CountryId,
    string? State,
    string? Telephone,
    string? Fax,
    string? Webpage,
    string? Email,
    string? ContactPerson,
    string? BankName,
    string? BankAccountNo,
    string? LogoFileName,
    DateTime? ModifiedDate)
{
    internal static CertificationBodyResponse From(CertificationBodyEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Acronym,
            entity.Notes,
            entity.RepresentingCountryId,
            entity.Address1,
            entity.Address2,
            entity.City,
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
            entity.Logo?.FileName,
            entity.SysDateModified);
}

public class GetCertificationBodyByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCertificationBodyByIdQuery, CertificationBodyResponse>
{
    public async Task<CertificationBodyResponse> Handle(
        GetCertificationBodyByIdQuery request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        var entity = await db.CertificationBodies
            .AsNoTracking()
            .FirstOrDefaultAsync(cb => cb.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Certification body not found.");

        return CertificationBodyResponse.From(entity);
    }
}
