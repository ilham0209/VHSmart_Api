using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// Edit a concept and save its "List of Menu" (spec 9.3: the modal fields, then the linked
// menus "then Save"). Same field rules as Create (kept in each feature file on purpose,
// CodingRules 4); the menu list is REQUIRED as a payload (it may be empty - a concept needs
// no menu in the spec) so a client that only edits the name can never unlink by omission.
// The pair list is reconciled like every other child list in this codebase, with the PD-02
// stance for the mapping status: a pair that drops off the list keeps its row as INACTIVE,
// a pair already present is set back to ACTIVE, a new pair is inserted as ACTIVE - never two
// live rows for one pair (Database.md 9 UQ). The tenant filter makes an unknown or foreign
// concept answer 404.
public record UpdateMenuConceptCommand(
    Guid Id,
    string? Name,
    string? Description,
    IReadOnlyList<Guid>? MenuIds) : IRequest<MenuConceptResponse>;

public class UpdateMenuConceptValidator : AbstractValidator<UpdateMenuConceptCommand>
{
    public UpdateMenuConceptValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Menu concept name is required.")
            .MaximumLength(200).WithMessage("Menu concept name must be 200 characters or fewer.");

        RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Description must be 1000 characters or fewer.");

        // "List of Menu" is the second Save of the modal - present, but allowed to be empty.
        // Every id must be a menu the caller can see: the existence check runs through the
        // "Accessible For" filter of CodingRules 7.3, so a menu another company never shared
        // with the caller answers "not found" instead of silently attaching.
        RuleFor(x => x.MenuIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("List of Menu is required.")
            .MustAsync(async (ids, ct) => ids is null || ids.Count == 0
                || await db.Menus.CountAsync(row => ids.Contains(row.Id), ct)
                    == ids.Distinct().Count())
            .WithMessage("Menu not found.");
    }
}

public class UpdateMenuConceptHandler(VHSmartDbContext db)
    : IRequestHandler<UpdateMenuConceptCommand, MenuConceptResponse>
{
    public async Task<MenuConceptResponse> Handle(
        UpdateMenuConceptCommand request,
        CancellationToken ct)
    {
        var entity = await db.MenuConcepts
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Menu concept not found.");

        MenuConceptData.Apply(entity, request);
        await ReconcileMenus(entity, request.MenuIds!, ct);
        await db.SaveChangesAsync(ct);

        return await MenuConceptResponseData.From(db, entity, ct);
    }

    // One live row per (concept, menu): an already known pair only ever changes its status,
    // an unknown pair gets its row, and no row is ever removed - so the filtered unique index
    // stays one LIVE row per pair and a later Save can flip the pair back to ACTIVE.
    private async Task ReconcileMenus(
        MenuConceptEntity entity,
        IReadOnlyList<Guid> requestedMenuIds,
        CancellationToken ct)
    {
        var wanted = requestedMenuIds.Distinct().ToHashSet();

        var existing = await db.MenuConceptMenus
            .Where(row => row.MenuConceptId == entity.Id)
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            var status = wanted.Contains(row.MenuId)
                ? MenuConceptMenuMappingStatus.Active
                : MenuConceptMenuMappingStatus.Inactive;
            if (row.MappingStatus != status)
                row.MappingStatus = status;
        }

        var present = existing
            .Select(row => row.MenuId)
            .ToHashSet();

        foreach (var menuId in wanted.Where(id => !present.Contains(id)))
            db.MenuConceptMenus.Add(new MenuConceptMenuEntity
            {
                CompanyId = entity.CompanyId,
                MenuConceptId = entity.Id,
                MenuId = menuId,
                MappingStatus = MenuConceptMenuMappingStatus.Active
            });
    }
}
