using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.ServiceProviders;

// Edit a Service Provider (spec 5.3). Same field rules as Create (kept in each feature file on
// purpose, CodingRules 4); the duplicate-name check excludes the row itself, and soft-deleted
// rows are invisible to the query, so a deleted provider's name is free again. The tenant
// filter makes another company's row answer 404.
public record UpdateServiceProviderCommand(
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
    string? BankAccountNo) : IRequest<ServiceProviderResponse>;

public class UpdateServiceProviderValidator : AbstractValidator<UpdateServiceProviderCommand>
{
    public UpdateServiceProviderValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class UpdateServiceProviderHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateServiceProviderCommand, ServiceProviderResponse>
{
    public async Task<ServiceProviderResponse> Handle(
        UpdateServiceProviderCommand request,
        CancellationToken ct)
    {
        var entity = await db.ServiceProviders
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Service provider not found.");

        var duplicate = await db.ServiceProviders.AnyAsync(row =>
            row.Id != entity.Id
            && row.CompanyId == user.CompanyId
            && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A service provider with this name already exists.");

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Address = request.Address;
        entity.Postcode = request.Postcode;
        entity.CountryId = request.CountryId;
        entity.State = request.State;
        entity.Telephone = request.Telephone;
        entity.Fax = request.Fax;
        entity.Webpage = request.Webpage;
        entity.Email = request.Email;
        entity.ContactPerson = request.ContactPerson;
        entity.BankName = request.BankName;
        entity.BankAccountNo = request.BankAccountNo;
        await db.SaveChangesAsync(ct);

        return ServiceProviderResponse.From(entity);
    }
}
