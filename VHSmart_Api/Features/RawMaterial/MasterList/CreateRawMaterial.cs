using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Add a raw material (spec 10.2 form, [MANUAL] mandatory list): Ingredient Status, Ingredient,
// Ingredient Source, Manufacturer and Accessible For. Every reference is checked against the
// row it must come from - the two general-data dropdowns are the caller's own PRODUCT rows
// (spec 5.1 reference data is per company), the manufacturer must be a row the caller can see
// (it is the owner - Database.md 8 says a handler checks the referenced row belongs to the same
// company) and Accessible For must name at least one live company. CompanyId comes from the JWT
// only (CodingRules 8.1).
// D-17: the ingredient code is unique per company among live rows - the message is ours.
public record CreateRawMaterialCommand(
    RawMaterialCategory? Category,
    Guid? IngredientStatusId,
    string? Ingredient,
    string? IngredientCode,
    string? CommercialName,
    string? ScientificName,
    Guid? IngredientSourceId,
    Guid? ManufacturerSupplierId,
    bool IsPackagingMaterial,
    IReadOnlyList<Guid>? AccessibleCompanyIds) : IRequest<RawMaterialResponse>;

public class CreateRawMaterialValidator : AbstractValidator<CreateRawMaterialCommand>
{
    // The Category literals of spec 5.1 / GeneralDataCatalog (PRODUCT group).
    private const string IngredientStatusCategory = "Ingredient Status";

    private const string IngredientSourceCategory = "Ingredient Source";

    public CreateRawMaterialValidator(VHSmartDbContext db, ICurrentUser user)
    {
        // Travels as a nullable enum so a missing value fails instead of silently becoming
        // Core (the enum's zero value).
        RuleFor(x => x.Category)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Category is required.")
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithMessage("Category is invalid.");

        RuleFor(x => x.IngredientStatusId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Ingredient status is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Category == IngredientStatusCategory,
                ct))
            .WithMessage("Ingredient status not found.");

        RuleFor(x => x.Ingredient)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Ingredient is required.")
            .MaximumLength(200);
        RuleFor(x => x.IngredientCode).MaximumLength(50);
        RuleFor(x => x.CommercialName).MaximumLength(200);
        RuleFor(x => x.ScientificName).MaximumLength(200);

        RuleFor(x => x.IngredientSourceId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Ingredient source is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Category == IngredientSourceCategory,
                ct))
            .WithMessage("Ingredient source not found.");

        RuleFor(x => x.ManufacturerSupplierId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Manufacturer is required.")
            .MustAsync(async (id, ct) => await db.ManufacturerSuppliers.AnyAsync(
                row => row.Id == id, ct))
            .WithMessage("Manufacturer not found.");

        // "Accessible For*" >= 1 row (spec 10.2, Database.md 8).
        RuleFor(x => x.AccessibleCompanyIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Accessible For is required.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("Accessible For is required.")
            .MustAsync(async (ids, ct) => ids is null || ids.Count == 0
                || await db.Companies.CountAsync(company => ids.Contains(company.Id), ct)
                    == ids.Distinct().Count())
            .WithMessage("Company not found.");
    }
}

public class CreateRawMaterialHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateRawMaterialCommand, RawMaterialResponse>
{
    public async Task<RawMaterialResponse> Handle(
        CreateRawMaterialCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;
        var entity = RawMaterialData.Apply(new RawMaterialEntity
        {
            CompanyId = companyId
        }, request);

        // D-17: unique per company among the rows the caller owns (the shared ones belong to
        // another company) - soft-deleted rows are already filtered out. A blank code is stored
        // as NULL and is never unique, so it is skipped here too.
        if (entity.IngredientCode is not null)
        {
            var duplicate = await db.RawMaterials.AnyAsync(row =>
                row.CompanyId == companyId && row.IngredientCode == entity.IngredientCode, ct);
            if (duplicate)
                throw new ConflictException("An ingredient with this code already exists.");
        }

        db.RawMaterials.Add(entity);

        foreach (var companyIdToShare in request.AccessibleCompanyIds!.Distinct())
            db.RawMaterialAccessibleCompanies.Add(new RawMaterialAccessibleCompanyEntity
            {
                RawMaterialId = entity.Id,
                AccessibleCompanyId = companyIdToShare
            });

        await db.SaveChangesAsync(ct);

        return await RawMaterialResponseData.From(db, entity, ct);
    }
}
