using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Companies;

// Company detail / form state (spec 6.3 edit, spec 7.1): every column the form shows plus the
// linked brands. Create and Update reload through LoadAsync so all three answers are identical.
public record GetCompanyByIdQuery(Guid Id) : IRequest<CompanyResponse>;

public record CompanyBrandResponse(Guid Id, string Name);

public record CompanyResponse(
    Guid Id,
    string Name,
    Guid CertificationBodyId,
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
    bool IsActive,
    List<CompanyBrandResponse> Brands,
    DateTime? ModifiedDate)
{
    internal static CompanyResponse From(CompanyEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.CertificationBodyId,
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
            entity.IsActive,
            entity.Brands
                .Where(link => link.Brand is not null)
                .OrderBy(link => link.Brand!.Name, StringComparer.Ordinal)
                .Select(link => new CompanyBrandResponse(link.BrandId, link.Brand!.Name))
                .ToList(),
            entity.SysDateModified);

    // Re-read with the brand links and their names, so Create / Update / GetById answer alike.
    // The global filters drop soft-deleted links and soft-deleted brand rows (CodingRules 7.1).
    internal static async Task<CompanyResponse> LoadAsync(
        VHSmartDbContext db,
        Guid id,
        CancellationToken ct)
    {
        var entity = await db.Companies
            .AsNoTracking()
            .Include(company => company.Brands)
                .ThenInclude(link => link.Brand)
            .FirstOrDefaultAsync(company => company.Id == id, ct);

        return entity is null
            ? throw new NotFoundException("Company not found.")
            : From(entity);
    }
}

public class GetCompanyByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCompanyByIdQuery, CompanyResponse>
{
    public async Task<CompanyResponse> Handle(
        GetCompanyByIdQuery request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage companies.");

        return await CompanyResponse.LoadAsync(db, request.Id, ct);
    }
}
