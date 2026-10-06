using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// Add a menu (spec 9.2 modal "Add New Menu"): Menu Name*, Menu Category*, Start Date, End
// Date, Description, Accessible For* ("List of Company*") and "List of Raw Materials*" are
// required - a menu needs at least one company and at least one raw material (spec 9.2,
// [CONFIRMED]). Every reference is checked against the row it must come from: the category is
// the caller's own COMPANY / "Menu Category" General Data (spec 5.1 reference data is per
// company), a company must be a live row of ComCompanies (the picker is not tenant scoped -
// sharing means naming other companies), and a raw material must be one the caller can see
// under the CodingRules 7.3 filter. CompanyId comes from the JWT only (CodingRules 8.1).
// The name is unique per company + category (Database.md 9, spec 9.2) - the handler answers
// 409 before the row changes.
public record CreateMenuCommand(
    string? Name,
    Guid? CategoryId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Description,
    IReadOnlyList<Guid>? AccessibleCompanyIds,
    IReadOnlyList<Guid>? RawMaterialIds) : IRequest<MenuResponse>;

public class CreateMenuValidator : AbstractValidator<CreateMenuCommand>
{
    public CreateMenuValidator(VHSmartDbContext db, ICurrentUser user)
    {
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

        // "List of Raw Materials*" >= 1 row (spec 9.2). The existence check runs through the
        // visibility filter of CodingRules 7.3, so a material another company never shared with
        // the caller answers "not found" instead of silently attaching.
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

public class CreateMenuHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateMenuCommand, MenuResponse>
{
    public async Task<MenuResponse> Handle(CreateMenuCommand request, CancellationToken ct)
    {
        var companyId = user.CompanyId;
        var entity = MenuData.Apply(new MenuEntity
        {
            CompanyId = companyId,
            Status = MenuStatus.Active
        }, request);

        // UQ (CompanyId, CategoryId, Name) among live rows (Database.md 9): the message is
        // ours, the spec states the rule but prints no text for it.
        var duplicate = await db.Menus.AnyAsync(row =>
            row.CompanyId == companyId
            && row.CategoryId == entity.CategoryId
            && row.Name == entity.Name, ct);
        if (duplicate)
            throw new ConflictException("A menu with this name already exists.");

        db.Menus.Add(entity);

        foreach (var companyIdToShare in request.AccessibleCompanyIds!.Distinct())
            db.MenuAccessibleCompanies.Add(new MenuAccessibleCompanyEntity
            {
                MenuId = entity.Id,
                AccessibleCompanyId = companyIdToShare
            });

        foreach (var rawMaterialId in request.RawMaterialIds!.Distinct())
            db.MenuRawMaterials.Add(new MenuRawMaterialEntity
            {
                CompanyId = companyId,
                MenuId = entity.Id,
                RawMaterialId = rawMaterialId
            });

        await db.SaveChangesAsync(ct);

        return await MenuResponseData.From(db, entity, ct);
    }
}
