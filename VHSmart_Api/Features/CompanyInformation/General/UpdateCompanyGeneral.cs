using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.General;

// Save button of Company > General (spec 7.1). The row edited is always the caller's own
// company - the command carries no id, so no request can target another tenant.
// CertificationBodyId, IsActive and the brand links are deliberately absent: the spec 7.1
// form does not show them, the CB list is Admin-only and Database.md assigns the brand links
// to Manage Companies (spec 6.3, platform admin).
// Required fields = the Database.md markers plus the spec 7.1 client-side list (legacy only
// checked them in the browser - a defect CodingRules 8.3 forbids copying).
public record UpdateCompanyGeneralCommand(
    string Name,
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
    string? Market) : IRequest<CompanyGeneralResponse>;

public class UpdateCompanyGeneralValidator : AbstractValidator<UpdateCompanyGeneralCommand>
{
    public UpdateCompanyGeneralValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
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
    }
}

public class UpdateCompanyGeneralHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCompanyGeneralCommand, CompanyGeneralResponse>
{
    public async Task<CompanyGeneralResponse> Handle(
        UpdateCompanyGeneralCommand request,
        CancellationToken ct)
    {
        // CompanyEntity is the tenant root, so the global filter only hides soft-deleted rows;
        // the explicit id match is what keeps the edit on the caller's own company.
        var entity = await db.Companies
            .FirstOrDefaultAsync(company => company.Id == user.CompanyId, ct)
            ?? throw new NotFoundException("Company not found.");

        var duplicateNumber = await db.Companies.AnyAsync(
            company => company.Id != entity.Id
                && company.BusinessRegistrationNo == request.BusinessRegistrationNo,
            ct);
        if (duplicateNumber)
            throw new ConflictException(
                "A company with this business registration number already exists.");

        var duplicateEmail = await db.Companies.AnyAsync(
            company => company.Id != entity.Id && company.Email == request.Email, ct);
        if (duplicateEmail)
            throw new ConflictException("A company with this e-mail already exists.");

        entity.Name = request.Name;
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

        await db.SaveChangesAsync(ct);

        return await CompanyGeneralResponse.LoadAsync(db, entity.Id, ct);
    }
}
