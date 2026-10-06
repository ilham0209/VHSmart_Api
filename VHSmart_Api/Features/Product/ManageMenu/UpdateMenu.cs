using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// Edit a menu (spec 9.2). Same field rules as Create (kept in each feature file on purpose,
// CodingRules 4); the name check excludes the row itself and soft-deleted rows are invisible,
// so a freed name can be reused within its category. Both child lists are reconciled like
// every other child list in this codebase: a row that is no longer selected is soft deleted, a
// row already present is left alone, a new one is inserted. The visibility filter (CodingRules
// 7.3) makes an unknown or invisible menu answer 404, and sharing stays read-only - a menu
// another company shared with the caller cannot be edited (MenuData.EnsureOwner).
public record UpdateMenuCommand(
    Guid Id,
    string? Name,
    Guid? CategoryId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Description,
    IReadOnlyList<Guid>? AccessibleCompanyIds,
    IReadOnlyList<Guid>? RawMaterialIds) : IRequest<MenuResponse>;

public class UpdateMenuValidator : AbstractValidator<UpdateMenuCommand>
{
    public UpdateMenuValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Menu name is required.")
            .MaximumLength(200).WithMessage("Menu name must be 200 characters or fewer.");

        RuleFor(x => x.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Menu category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.COMPANY
                    && row.Category == MenuGeneralDataCategory.MenuCategory,
                ct))
            .WithMessage("Menu category not found.");

        RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Description must be 1000 characters or fewer.");

        // "List of Company*" >= 1 row (spec 9.2).
        RuleFor(x => x.AccessibleCompanyIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("List of Company is required.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("List of Company is required.")
            .MustAsync(async (ids, ct) => ids is null || ids.Count == 0
                || await db.Companies.CountAsync(company => ids.Contains(company.Id), ct)
                    == ids.Distinct().Count())
            .WithMessage("Company not found.");

        // "List of Raw Materials*" >= 1 row (spec 9.2).
        RuleFor(x => x.RawMaterialIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("List of Raw Materials is required.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("List of Raw Materials is required.")
            .MustAsync(async (ids, ct) => ids is null || ids.Count == 0
                || await db.RawMaterials.CountAsync(row => ids.Contains(row.Id), ct)
                    == ids.Distinct().Count())
            .WithMessage("Raw material not found.");
    }
}

public class UpdateMenuHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateMenuCommand, MenuResponse>
{
    public async Task<MenuResponse> Handle(UpdateMenuCommand request, CancellationToken ct)
    {
        var entity = await db.Menus
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Menu not found.");

        // The list shows menus shared WITH the caller too; sharing is read-only (MenuData).
        MenuData.EnsureOwner(entity, user);

        // Exactly the category + name pair Apply will store, so a collision is detected before
        // the row changes (UQ (CompanyId, CategoryId, Name) among live rows, Database.md 9).
        var categoryId = request.CategoryId!.Value;
        var duplicate = await db.Menus.AnyAsync(row =>
            row.Id != entity.Id
            && row.CompanyId == user.CompanyId
            && row.CategoryId == categoryId
            && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A menu with this name already exists.");

        MenuData.Apply(entity, request);
        await ReconcileAccessibleCompanies(entity, request.AccessibleCompanyIds!, ct);
        await ReconcileRawMaterials(entity, request.RawMaterialIds!, ct);
        await db.SaveChangesAsync(ct);

        return await MenuResponseData.From(db, entity, ct);
    }

    private async Task ReconcileAccessibleCompanies(
        MenuEntity entity,
        IReadOnlyList<Guid> requestedCompanyIds,
        CancellationToken ct)
    {
        var wanted = requestedCompanyIds.Distinct().ToHashSet();

        var existing = await db.MenuAccessibleCompanies
            .Where(row => row.MenuId == entity.Id)
            .ToListAsync(ct);

        foreach (var row in existing.Where(row => !wanted.Contains(row.AccessibleCompanyId)))
            db.MenuAccessibleCompanies.Remove(row);

        var present = existing
            .Select(row => row.AccessibleCompanyId)
            .ToHashSet();

        foreach (var companyId in wanted.Where(id => !present.Contains(id)))
            db.MenuAccessibleCompanies.Add(new MenuAccessibleCompanyEntity
            {
                MenuId = entity.Id,
                AccessibleCompanyId = companyId
            });
    }

    // Same shape for "List of Raw Materials", with the menu's own CompanyId on every link row:
    // a dropped pair is soft deleted (CodingRules 7.1) so the filtered unique index
    // (MenuId, RawMaterialId) stays one LIVE row per pair and a later save can link it again.
    private async Task ReconcileRawMaterials(
        MenuEntity entity,
        IReadOnlyList<Guid> requestedRawMaterialIds,
        CancellationToken ct)
    {
        var wanted = requestedRawMaterialIds.Distinct().ToHashSet();

        var existing = await db.MenuRawMaterials
            .Where(row => row.MenuId == entity.Id)
            .ToListAsync(ct);

        foreach (var row in existing.Where(row => !wanted.Contains(row.RawMaterialId)))
            db.MenuRawMaterials.Remove(row);

        var present = existing
            .Select(row => row.RawMaterialId)
            .ToHashSet();

        foreach (var rawMaterialId in wanted.Where(id => !present.Contains(id)))
            db.MenuRawMaterials.Add(new MenuRawMaterialEntity
            {
                CompanyId = entity.CompanyId,
                MenuId = entity.Id,
                RawMaterialId = rawMaterialId
            });
    }
}
