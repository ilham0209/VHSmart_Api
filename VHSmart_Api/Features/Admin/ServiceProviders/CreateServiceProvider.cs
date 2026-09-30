using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.ServiceProviders;

// Add a Service Provider (spec 5.3 form). Required fields follow Database.md 3 (Name, Address,
// Postcode, Country, State, Telephone, Email, ContactPerson) and match the asterisks on the
// spec form; the country id is checked against AdmCountries so a stale dropdown value answers
// 400 instead of an FK error. CompanyId comes from the JWT only (CodingRules 8.1) - the
// "Company Id" field of the spec form is read-only display.
// Duplicate rule (spec 21.9): the same Name within the company is refused while the row is
// live; Database.md 3 defines no unique index, so the check lives here like on the CB screen.
public record CreateServiceProviderCommand(
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
    string? BankAccountNo) : IRequest<ServiceProviderResponse>;

public class CreateServiceProviderValidator : AbstractValidator<CreateServiceProviderCommand>
{
    public CreateServiceProviderValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Address)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500);
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

public class CreateServiceProviderHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateServiceProviderCommand, ServiceProviderResponse>
{
    public async Task<ServiceProviderResponse> Handle(
        CreateServiceProviderCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var duplicate = await db.ServiceProviders.AnyAsync(row =>
            row.CompanyId == companyId && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A service provider with this name already exists.");

        var entity = new ServiceProviderEntity
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            Address = request.Address,
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
        db.ServiceProviders.Add(entity);
        await db.SaveChangesAsync(ct);

        return ServiceProviderResponse.From(entity);
    }
}
