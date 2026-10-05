using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Edit a raw material (spec 10.2). Same field rules as Create (kept in each feature file on
// purpose, CodingRules 4); the ingredient-code check excludes the row itself and soft-deleted
// rows are invisible, so a freed code can be reused. The "Accessible For" list is reconciled
// like every other child list in this codebase: rows that are no longer selected are soft
// deleted, rows already present are left alone, new ones are inserted. The visibility filter
// (CodingRules 7.3) makes an unknown or foreign row answer 404.
public record UpdateRawMaterialCommand(
    Guid Id,
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

public class UpdateRawMaterialValidator : AbstractValidator<UpdateRawMaterialCommand>
{
    // The Category literals of spec 5.1 / GeneralDataCatalog (PRODUCT group).
    private const string IngredientStatusCategory = "Ingredient Status";

    private const string IngredientSourceCategory = "Ingredient Source";

    public UpdateRawMaterialValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

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

public class UpdateRawMaterialHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateRawMaterialCommand, RawMaterialResponse>
{
    public async Task<RawMaterialResponse> Handle(
        UpdateRawMaterialCommand request,
        CancellationToken ct)
    {
        var entity = await db.RawMaterials
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Raw material not found.");

        // The list shows rows shared WITH the caller too; sharing is read-only (see
        // RawMaterialData.EnsureOwner).
        RawMaterialData.EnsureOwner(entity, user);

        // Exactly the code Apply will store, so a collision is detected before the row changes.
        var ingredientCode = string.IsNullOrWhiteSpace(request.IngredientCode)
            ? null
            : request.IngredientCode.Trim();

        if (ingredientCode is not null)
        {
            var duplicate = await db.RawMaterials.AnyAsync(row =>
                row.Id != entity.Id
                && row.CompanyId == user.CompanyId
                && row.IngredientCode == ingredientCode, ct);
            if (duplicate)
                throw new ConflictException("An ingredient with this code already exists.");
        }

        RawMaterialData.Apply(entity, request);
        await ReconcileAccessibleCompanies(entity, request.AccessibleCompanyIds!, ct);
        await db.SaveChangesAsync(ct);

        return await RawMaterialResponseData.From(db, entity, ct);
    }

    private async Task ReconcileAccessibleCompanies(
        RawMaterialEntity entity,
        IReadOnlyList<Guid> requestedCompanyIds,
        CancellationToken ct)
    {
        var wanted = requestedCompanyIds.Distinct().ToHashSet();

        var existing = await db.RawMaterialAccessibleCompanies
            .Where(row => row.RawMaterialId == entity.Id)
            .ToListAsync(ct);

        foreach (var row in existing.Where(row => !wanted.Contains(row.AccessibleCompanyId)))
            db.RawMaterialAccessibleCompanies.Remove(row);

        var present = existing
            .Select(row => row.AccessibleCompanyId)
            .ToHashSet();

        foreach (var companyId in wanted.Where(id => !present.Contains(id)))
            db.RawMaterialAccessibleCompanies.Add(new RawMaterialAccessibleCompanyEntity
            {
                RawMaterialId = entity.Id,
                AccessibleCompanyId = companyId
            });
    }
}
