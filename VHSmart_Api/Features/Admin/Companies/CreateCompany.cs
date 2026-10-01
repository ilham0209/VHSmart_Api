using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Companies;

// Add Company (spec 6.3 "+ add", Super-User only - D-07). Required fields follow Database.md 3
// plus the client-side required list of spec 7.1 (Registration Type and Owner Status are
// required there although the columns are nullable). Stale dropdown values (CB, country,
// scheme, brand) answer 400 from the validator instead of an FK error; brand rows themselves
// are never written here - D-20 keeps them in AdmGeneralData, this command only links them.
// Uniqueness of the registration number and the e-mail is spec 6.1, enforced with 409.
public record CreateCompanyCommand(
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

public class CreateCompanyValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyValidator(VHSmartDbContext db)
    {
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
        // Database.md 3 and spec 7.1 legacy: strictly earlier than today (UTC server date).
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

public class CreateCompanyHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateCompanyCommand, CompanyResponse>
{
    public async Task<CompanyResponse> Handle(
        CreateCompanyCommand request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage companies.");

        // Spec 6.1: the registration number and the e-mail must be unique (Database.md 3 has
        // the filtered unique indexes as a backstop for concurrent requests).
        var duplicateNumber = await db.Companies.AnyAsync(
            company => company.BusinessRegistrationNo == request.BusinessRegistrationNo, ct);
        if (duplicateNumber)
            throw new ConflictException(
                "A company with this business registration number already exists.");

        var duplicateEmail = await db.Companies.AnyAsync(
            company => company.Email == request.Email, ct);
        if (duplicateEmail)
            throw new ConflictException("A company with this e-mail already exists.");

        var entity = new CompanyEntity
        {
            Name = request.Name,
            CertificationBodyId = request.CertificationBodyId,
            RegistrationType = request.RegistrationType,
            BusinessRegistrationNo = request.BusinessRegistrationNo,
            OwnerStatus = request.OwnerStatus,
            Address1 = request.Address1,
            Address2 = request.Address2,
            Address3 = request.Address3,
            PostCode = request.PostCode,
            City = request.City,
            District = request.District,
            State = request.State,
            CountryId = request.CountryId,
            Telephone = request.Telephone,
            Fax = request.Fax,
            SchemeId = request.SchemeId,
            IndustrySize = request.IndustrySize,
            WebsiteUrl = request.WebsiteUrl,
            Email = request.Email,
            DateOfEstablishment = request.DateOfEstablishment,
            MainProductsServices = request.MainProductsServices,
            Market = request.Market,
            IsActive = request.IsActive ?? true
        };
        db.Companies.Add(entity);

        // Distinct so a repeated id cannot trip the (CompanyId, BrandId) unique pair.
        foreach (var brandId in (request.BrandIds ?? []).Distinct())
            db.CompanyBrands.Add(new CompanyBrandEntity
            {
                CompanyId = entity.Id,
                BrandId = brandId
            });

        await db.SaveChangesAsync(ct);

        return await CompanyResponse.LoadAsync(db, entity.Id, ct);
    }
}
