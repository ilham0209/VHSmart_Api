using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Companies;

// Edit company (spec 6.3 "Edit company - select Brand - Add - Save", Super-User only - D-07).
// Same fields and validations as Create; PUT replaces every column (an omitted optional string
// clears itself), while an omitted IsActive leaves the flag as it was - it has no empty state.
// The brand links are diffed against the saved set: dropped links are soft-deleted (CodingRules
// 7.1) so the (CompanyId, BrandId) pair is free again, new ones are added.
public record UpdateCompanyCommand(
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
    bool? IsActive,
    List<Guid>? BrandIds) : IRequest<CompanyResponse>;

public class UpdateCompanyValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.CertificationBodyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Certification body is required.")
            .MustAsync((id, ct) => db.CertificationBodies.AnyAsync(cb => cb.Id == id, ct))
            .WithMessage("Certification body not found.");
        RuleFor(x => x.RegistrationType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Registration type is required.")
            .MaximumLength(100);
        RuleFor(x => x.BusinessRegistrationNo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Business registration number is required.")
            .MaximumLength(50);
        RuleFor(x => x.OwnerStatus)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Owner status is required.")
            .MaximumLength(100);
        RuleFor(x => x.Address1)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address line 1 is required.")
            .MaximumLength(200);
        RuleFor(x => x.Address2)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address line 2 is required.")
            .MaximumLength(200);
        RuleFor(x => x.Address3).MaximumLength(200);
        RuleFor(x => x.PostCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Postcode is required.")
            .MaximumLength(20);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.District)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("District is required.")
            .MaximumLength(100);
        RuleFor(x => x.State)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100);
        RuleFor(x => x.CountryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Country is required.")
            .MustAsync((id, ct) => db.Countries.AnyAsync(country => country.Id == id, ct))
            .WithMessage("Country not found.");
        RuleFor(x => x.Telephone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Telephone is required.")
            .MaximumLength(30);
        RuleFor(x => x.Fax).MaximumLength(30);
        RuleFor(x => x.SchemeId)
            .MustAsync(async (id, ct) =>
                id is null || await db.Schemes.AnyAsync(scheme => scheme.Id == id, ct))
            .WithMessage("Scheme not found.");
        RuleFor(x => x.IndustrySize).MaximumLength(100);
        RuleFor(x => x.WebsiteUrl).MaximumLength(200);
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254);
        RuleFor(x => x.DateOfEstablishment)
            .Must(date => date is null || date.Value.Date < DateTime.UtcNow.Date)
            .WithMessage("Date of establishment must be strictly earlier than today.");
        RuleFor(x => x.MainProductsServices).MaximumLength(500);
        RuleFor(x => x.Market).MaximumLength(100);
        RuleForEach(x => x.BrandIds)
            .MustAsync((brandId, ct) => db.GeneralData.AnyAsync(
                row => row.Id == brandId
                    && row.Group == GeneralDataGroup.COMPANY
                    && row.Category == "Brand",
                ct))
            .WithMessage("Brand not found.");
    }
}

public class UpdateCompanyHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCompanyCommand, CompanyResponse>
{
    public async Task<CompanyResponse> Handle(
        UpdateCompanyCommand request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage companies.");

        var entity = await db.Companies
            .Include(company => company.Brands)
            .FirstOrDefaultAsync(company => company.Id == request.Id, ct)
            ?? throw new NotFoundException("Company not found.");

        // Spec 6.1 uniqueness, excluding the row being edited.
        var duplicateNumber = await db.Companies.AnyAsync(
            company => company.Id != request.Id
                && company.BusinessRegistrationNo == request.BusinessRegistrationNo,
            ct);
        if (duplicateNumber)
            throw new ConflictException(
                "A company with this business registration number already exists.");

        var duplicateEmail = await db.Companies.AnyAsync(
            company => company.Id != request.Id && company.Email == request.Email, ct);
        if (duplicateEmail)
            throw new ConflictException("A company with this e-mail already exists.");

        entity.Name = request.Name;
        entity.CertificationBodyId = request.CertificationBodyId;
        entity.RegistrationType = request.RegistrationType;
        entity.BusinessRegistrationNo = request.BusinessRegistrationNo;
        entity.OwnerStatus = request.OwnerStatus;
        entity.Address1 = request.Address1;
        entity.Address2 = request.Address2;
        entity.Address3 = request.Address3;
        entity.PostCode = request.PostCode;
        entity.City = request.City;
        entity.District = request.District;
        entity.State = request.State;
        entity.CountryId = request.CountryId;
        entity.Telephone = request.Telephone;
        entity.Fax = request.Fax;
        entity.SchemeId = request.SchemeId;
        entity.IndustrySize = request.IndustrySize;
        entity.WebsiteUrl = request.WebsiteUrl;
        entity.Email = request.Email;
        entity.DateOfEstablishment = request.DateOfEstablishment;
        entity.MainProductsServices = request.MainProductsServices;
        entity.Market = request.Market;
        if (request.IsActive is not null)
            entity.IsActive = request.IsActive.Value;

        var desired = (request.BrandIds ?? []).Distinct().ToHashSet();
        foreach (var link in entity.Brands.Where(link => !desired.Contains(link.BrandId)))
            db.CompanyBrands.Remove(link);
        foreach (var brandId in desired.Where(
                     brandId => entity.Brands.All(link => link.BrandId != brandId)))
            db.CompanyBrands.Add(new CompanyBrandEntity
            {
                CompanyId = entity.Id,
                BrandId = brandId
            });

        await db.SaveChangesAsync(ct);

        return await CompanyResponse.LoadAsync(db, entity.Id, ct);
    }
}
