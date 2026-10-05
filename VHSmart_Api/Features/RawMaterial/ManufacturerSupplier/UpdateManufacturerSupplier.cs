using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Edit a Manufacturer & Supplier (spec 10.1). Same field rules as Create (kept in each feature
// file on purpose, CodingRules 4); the e-mail checks exclude the row itself, and soft-deleted
// rows are invisible to the query, so a deleted address is free again. The tenant filter makes
// another company's row answer 404.
public record UpdateManufacturerSupplierCommand(
    Guid Id,
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

public class UpdateManufacturerSupplierValidator
    : AbstractValidator<UpdateManufacturerSupplierCommand>
{
    // The Category literal of spec 5.1 / GeneralDataCatalog (COMPANY / Manufacturer Type).
    private const string ManufacturerTypeCategory = "Manufacturer Type";

    public UpdateManufacturerSupplierValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

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

    private static bool ManufacturerPartRequired(UpdateManufacturerSupplierCommand command) =>
        command.Type is ManufacturerSupplierType.ManufacturerOnly or ManufacturerSupplierType.Both;

    private static bool SupplierPartRequired(UpdateManufacturerSupplierCommand command) =>
        command.Type is ManufacturerSupplierType.SupplierOnly or ManufacturerSupplierType.Both;
}

public class UpdateManufacturerSupplierHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateManufacturerSupplierCommand, ManufacturerSupplierResponse>
{
    public async Task<ManufacturerSupplierResponse> Handle(
        UpdateManufacturerSupplierCommand request,
        CancellationToken ct)
    {
        var entity = await db.ManufacturerSuppliers
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Manufacturer and supplier not found.");

        // Exactly the e-mails Apply will store (the half the new Type does not use is dropped),
        // so a collision is detected before anything on the row changes; each half has its own
        // per-company rule (spec 21.9).
        var manufacturerEmail = ManufacturerSupplierData.KeepsManufacturer(request.Type)
            ? request.ManufacturerEmail
            : null;
        var supplierEmail = ManufacturerSupplierData.KeepsSupplier(request.Type)
            ? request.SupplierEmail
            : null;

        if (manufacturerEmail is not null)
        {
            var duplicateManufacturer = await db.ManufacturerSuppliers.AnyAsync(row =>
                row.Id != entity.Id
                && row.CompanyId == user.CompanyId
                && row.ManufacturerEmail == manufacturerEmail, ct);
            if (duplicateManufacturer)
                throw new ConflictException("A manufacturer with this e-mail already exists.");
        }

        if (supplierEmail is not null)
        {
            var duplicateSupplier = await db.ManufacturerSuppliers.AnyAsync(row =>
                row.Id != entity.Id
                && row.CompanyId == user.CompanyId
                && row.SupplierEmail == supplierEmail, ct);
            if (duplicateSupplier)
                throw new ConflictException("A supplier with this e-mail already exists.");
        }

        ManufacturerSupplierData.Apply(entity, request);
        await db.SaveChangesAsync(ct);

        return ManufacturerSupplierResponse.From(entity);
    }
}
