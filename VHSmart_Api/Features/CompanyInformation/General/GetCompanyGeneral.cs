using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.General;

// Company > General (spec 7.1): the caller's own General Information form. The company id is
// always the JWT's active company (CodingRules 7.4) - no id appears in the URL or body, so no
// request can read or edit another tenant (the explicit id match also covers a ViewAll token).
// CertificationBodyName is included because the CB list behind it is Admin-only
// (Admin.CertificationBodies) and a company user could not otherwise resolve the id.
public record GetCompanyGeneralQuery : IRequest<CompanyGeneralResponse>;

public record CompanyGeneralResponse(
    Guid Id,
    string Name,
    Guid CertificationBodyId,
    string? CertificationBodyName,
    string? RegistrationType,
    string BusinessRegistrationNo,
    string? OwnerStatus,
    string Address1,
    string Address2,
    string? Address3,
    string PostCode,
    string? City,
    string District,
    string State,
    Guid CountryId,
    string Telephone,
    string? Fax,
    Guid? SchemeId,
    string? IndustrySize,
    string? WebsiteUrl,
    string Email,
    DateTime? DateOfEstablishment,
    string? MainProductsServices,
    string? Market,
    DateTime? ModifiedDate)
{
    internal static CompanyGeneralResponse From(CompanyEntity entity, string? certificationBodyName) =>
        new(
            entity.Id,
            entity.Name,
            entity.CertificationBodyId,
            certificationBodyName,
            entity.RegistrationType,
            entity.BusinessRegistrationNo,
            entity.OwnerStatus,
            entity.Address1,
            entity.Address2,
            entity.Address3,
            entity.PostCode,
            entity.City,
            entity.District,
            entity.State,
            entity.CountryId,
            entity.Telephone,
            entity.Fax,
            entity.SchemeId,
            entity.IndustrySize,
            entity.WebsiteUrl,
            entity.Email,
            entity.DateOfEstablishment,
            entity.MainProductsServices,
            entity.Market,
            entity.SysDateModified);

    internal static async Task<CompanyGeneralResponse> LoadAsync(
        VHSmartDbContext db,
        Guid companyId,
        CancellationToken ct)
    {
        var entity = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == companyId, ct)
            ?? throw new NotFoundException("Company not found.");

        var certificationBodyName = await db.CertificationBodies
            .AsNoTracking()
            .Where(body => body.Id == entity.CertificationBodyId)
            .Select(body => body.Name)
            .FirstOrDefaultAsync(ct);

        return From(entity, certificationBodyName);
    }
}

public class GetCompanyGeneralHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCompanyGeneralQuery, CompanyGeneralResponse>
{
    public Task<CompanyGeneralResponse> Handle(
        GetCompanyGeneralQuery request,
        CancellationToken ct) =>
        CompanyGeneralResponse.LoadAsync(db, user.CompanyId, ct);
}
