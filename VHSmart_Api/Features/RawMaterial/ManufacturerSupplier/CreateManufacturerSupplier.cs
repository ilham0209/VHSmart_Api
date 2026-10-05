using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Add a Manufacturer & Supplier (spec 10.1 form). Type decides which half must be filled:
// Database.md 8 makes the manufacturer part required unless the row is supplier-only and the
// supplier part required unless it is manufacturer-only; the asterisks on the spec form say the
// required fields of a half are Name, Address and Country. Country ids are checked against
// AdmCountries and the category against the caller's own Manufacturer Type general data so a
// stale dropdown value answers 400 instead of an FK error. CompanyId comes from the JWT only
// (CodingRules 8.1).
// E-mail rule (spec 10.1 legacy + 21.9): a manufacturer e-mail must be unique per company and a
// supplier e-mail must be unique per company - the messages are ours, the spec gives none.
// Manufacturer NAME is deliberately not unique: Database.md 8 defines no index for it and 21.9
// does not list it (only the e-mails).
public record CreateManufacturerSupplierCommand(
    ManufacturerSupplierType? Type,
    string? ManufacturerName,
    string? ManufacturerBusinessRegNo,
    Guid? ManufacturerTypeId,
    string? ManufacturerAddress,
    Guid? ManufacturerCountryId,
    string? ManufacturerPersonInCharge,
    string? ManufacturerContactNo,
    string? ManufacturerEmail,
    string? ManufacturerWebpage,
    string? SupplierName,
    string? SupplierAddress,
    Guid? SupplierCountryId,
    string? SupplierPersonInCharge,
    string? SupplierContactNo,
    string? SupplierEmail) : IRequest<ManufacturerSupplierResponse>;

public class CreateManufacturerSupplierValidator
    : AbstractValidator<CreateManufacturerSupplierCommand>
{
    // The Category literal of spec 5.1 / GeneralDataCatalog (COMPANY / Manufacturer Type).
    private const string ManufacturerTypeCategory = "Manufacturer Type";

    public CreateManufacturerSupplierValidator(VHSmartDbContext db, ICurrentUser user)
    {
        // Travels as a nullable enum so a missing value fails instead of silently becoming
        // ManufacturerOnly (the enum's zero value).
        RuleFor(x => x.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Type is required.")
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithMessage("Type is invalid.");

        RuleFor(x => x.ManufacturerName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Manufacturer name is required.")
            .MaximumLength(200)
            .When(ManufacturerPartRequired);
        RuleFor(x => x.ManufacturerBusinessRegNo).MaximumLength(50);
        RuleFor(x => x.ManufacturerAddress)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Manufacturer address is required.")
            .MaximumLength(500)
            .When(ManufacturerPartRequired);
        RuleFor(x => x.ManufacturerCountryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Country is required.")
            .MustAsync((id, ct) => db.Countries.AnyAsync(country => country.Id == id, ct))
            .WithMessage("Country not found.")
            .When(ManufacturerPartRequired);
        RuleFor(x => x.ManufacturerPersonInCharge).MaximumLength(200);
        RuleFor(x => x.ManufacturerContactNo).MaximumLength(30);
        RuleFor(x => x.ManufacturerEmail).MaximumLength(254);
        RuleFor(x => x.ManufacturerWebpage).MaximumLength(200);

        RuleFor(x => x.SupplierName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Supplier name is required.")
            .MaximumLength(200)
            .When(SupplierPartRequired);
        RuleFor(x => x.SupplierAddress)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Supplier address is required.")
            .MaximumLength(500)
            .When(SupplierPartRequired);
        RuleFor(x => x.SupplierCountryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Country is required.")
            .MustAsync((id, ct) => db.Countries.AnyAsync(country => country.Id == id, ct))
            .WithMessage("Country not found.")
            .When(SupplierPartRequired);
        RuleFor(x => x.SupplierPersonInCharge).MaximumLength(200);
        RuleFor(x => x.SupplierContactNo).MaximumLength(30);
        RuleFor(x => x.SupplierEmail).MaximumLength(254);

        // Category is optional on the spec form; when sent it must be one of the caller's own
        // Manufacturer Type rows (spec 5.1 reference data is per company).
        RuleFor(x => x.ManufacturerTypeId)
            .MustAsync(async (id, ct) => id is null || await db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Category == ManufacturerTypeCategory,
                ct))
            .WithMessage("Manufacturer type not found.");
    }

    private static bool ManufacturerPartRequired(CreateManufacturerSupplierCommand command) =>
        command.Type is ManufacturerSupplierType.ManufacturerOnly or ManufacturerSupplierType.Both;

    private static bool SupplierPartRequired(CreateManufacturerSupplierCommand command) =>
        command.Type is ManufacturerSupplierType.SupplierOnly or ManufacturerSupplierType.Both;
}

public class CreateManufacturerSupplierHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateManufacturerSupplierCommand, ManufacturerSupplierResponse>
{
    public async Task<ManufacturerSupplierResponse> Handle(
        CreateManufacturerSupplierCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;
        var entity = ManufacturerSupplierData.Apply(new ManufacturerSupplierEntity
        {
            CompanyId = companyId
        }, request);

        // ManufacturerSupplierData cleared the half the Type does not use, so only a surviving
        // e-mail can collide; each half has its own per-company rule (spec 21.9).
        if (entity.ManufacturerEmail is not null)
        {
            var duplicateManufacturer = await db.ManufacturerSuppliers.AnyAsync(row =>
                row.CompanyId == companyId && row.ManufacturerEmail == entity.ManufacturerEmail, ct);
            if (duplicateManufacturer)
                throw new ConflictException("A manufacturer with this e-mail already exists.");
        }

        if (entity.SupplierEmail is not null)
        {
            var duplicateSupplier = await db.ManufacturerSuppliers.AnyAsync(row =>
                row.CompanyId == companyId && row.SupplierEmail == entity.SupplierEmail, ct);
            if (duplicateSupplier)
                throw new ConflictException("A supplier with this e-mail already exists.");
        }

        db.ManufacturerSuppliers.Add(entity);
        await db.SaveChangesAsync(ct);

        return ManufacturerSupplierResponse.From(entity);
    }
}
