using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// Add a CB (spec 5.2 form, platform admin only - D-07). Required fields follow Database.md 3
// (Name, City, Postcode, Country, State, Telephone, Email, ContactPerson); the country ids are
// checked against AdmCountries so a stale dropdown value answers 400 instead of an FK error.
// The "Company Id" field of the spec form has no column - the table is global (Database.md 3).
public record CreateCertificationBodyCommand(
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
    string? BankAccountNo) : IRequest<CertificationBodyResponse>;

public class CreateCertificationBodyValidator : AbstractValidator<CreateCertificationBodyCommand>
{
    public CreateCertificationBodyValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.Acronym).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.RepresentingCountryId)
            .MustAsync(async (id, token) =>
                id is null || await db.Countries.AnyAsync(country => country.Id == id, token))
            .WithMessage("Representing country not found.");
        RuleFor(x => x.Address1).MaximumLength(200);
        RuleFor(x => x.Address2).MaximumLength(200);
        RuleFor(x => x.City)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100);
        RuleFor(x => x.Postcode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Postcode is required.")
            .MaximumLength(20);
        RuleFor(x => x.CountryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Country is required.")
            .MustAsync((id, ct) => db.Countries.AnyAsync(country => country.Id == id, ct))
            .WithMessage("Country not found.");
        RuleFor(x => x.State)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100);
        RuleFor(x => x.Telephone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Telephone is required.")
            .MaximumLength(30);
        RuleFor(x => x.Fax).MaximumLength(30);
        RuleFor(x => x.Webpage).MaximumLength(200);
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254);
        RuleFor(x => x.ContactPerson)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Contact person is required.")
            .MaximumLength(200);
        RuleFor(x => x.BankName).MaximumLength(200);
        RuleFor(x => x.BankAccountNo).MaximumLength(50);
    }
}

public class CreateCertificationBodyHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateCertificationBodyCommand, CertificationBodyResponse>
{
    public async Task<CertificationBodyResponse> Handle(
        CreateCertificationBodyCommand request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        // Database.md 3 recommends the duplicate-name check legacy did not have.
        var duplicate = await db.CertificationBodies
            .AnyAsync(cb => cb.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A certification body with this name already exists.");

        var entity = new CertificationBodyEntity
        {
            Name = request.Name,
            Acronym = request.Acronym,
            Notes = request.Notes,
            RepresentingCountryId = request.RepresentingCountryId,
            Address1 = request.Address1,
            Address2 = request.Address2,
            City = request.City,
            Postcode = request.Postcode,
            CountryId = request.CountryId,
            State = request.State,
            Telephone = request.Telephone,
            Fax = request.Fax,
            Webpage = request.Webpage,
            Email = request.Email,
            ContactPerson = request.ContactPerson,
            BankName = request.BankName,
            BankAccountNo = request.BankAccountNo
        };
        db.CertificationBodies.Add(entity);
        await db.SaveChangesAsync(ct);

        return CertificationBodyResponse.From(entity);
    }
}
