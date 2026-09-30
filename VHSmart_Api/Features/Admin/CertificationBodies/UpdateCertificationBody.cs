using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// Edit a CB (spec 5.2, platform admin only - D-07). Same field rules as Create (kept in each
// feature file on purpose, CodingRules 4); the duplicate-name check excludes the row itself
// and soft-deleted rows are invisible to the query, so a deleted CB's name is free again.
public record UpdateCertificationBodyCommand(
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
    string? BankAccountNo) : IRequest<CertificationBodyResponse>;

public class UpdateCertificationBodyValidator : AbstractValidator<UpdateCertificationBodyCommand>
{
    public UpdateCertificationBodyValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class UpdateCertificationBodyHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCertificationBodyCommand, CertificationBodyResponse>
{
    public async Task<CertificationBodyResponse> Handle(
        UpdateCertificationBodyCommand request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        var entity = await db.CertificationBodies
            .FirstOrDefaultAsync(cb => cb.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Certification body not found.");

        var duplicate = await db.CertificationBodies
            .AnyAsync(cb => cb.Id != entity.Id && cb.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A certification body with this name already exists.");

        entity.Name = request.Name;
        entity.Acronym = request.Acronym;
        entity.Notes = request.Notes;
        entity.RepresentingCountryId = request.RepresentingCountryId;
        entity.Address1 = request.Address1;
        entity.Address2 = request.Address2;
        entity.City = request.City;
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

        return CertificationBodyResponse.From(entity);
    }
}
