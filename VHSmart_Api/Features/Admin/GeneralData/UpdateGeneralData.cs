using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.GeneralData;

// Edit a dropdown value (spec 5.1, pencil action). The duplicate rule (spec 21.9) excludes the
// row itself; moving Group/Category is checked against the target slot. CompanyId is never
// editable - the row stays with its company (spec 3.3, CodingRules 8.1).
public record UpdateGeneralDataCommand(
    Guid Id,
    GeneralDataGroup? Group,
    string Category,
    string Name,
    string? Description) : IRequest<UpdateGeneralDataResponse>;

public record UpdateGeneralDataResponse(
    Guid Id,
    GeneralDataGroup Group,
    string Category,
    string Name,
    string? Description);

public class UpdateGeneralDataValidator : AbstractValidator<UpdateGeneralDataCommand>
{
    public UpdateGeneralDataValidator()
    {
        RuleFor(x => x.Group)
            .NotNull().WithMessage("Group is required.")
            .IsInEnum().WithMessage("Unknown group.");
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category must be 100 characters or fewer.")
            .Must((command, category) => command.Group is null
                || GeneralDataCatalog.BelongsTo(command.Group.Value, category))
            .WithMessage("The category does not belong to the selected group.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");
    }
}

public class UpdateGeneralDataHandler(VHSmartDbContext db)
    : IRequestHandler<UpdateGeneralDataCommand, UpdateGeneralDataResponse>
{
    public async Task<UpdateGeneralDataResponse> Handle(
        UpdateGeneralDataCommand request,
        CancellationToken ct)
    {
        if (request.Group is not { } group)
            throw new BusinessRuleException("Group is required.");

        var entity = await db.GeneralData
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("General data not found.");

        var duplicate = await db.GeneralData.AnyAsync(row =>
            row.Id != entity.Id
            && row.CompanyId == entity.CompanyId
            && row.Group == group
            && row.Category == request.Category
            && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException(
                "A record with this name already exists in the selected group and category.");

        entity.Group = group;
        entity.Category = request.Category;
        entity.Name = request.Name;
        entity.Description = request.Description;
        await db.SaveChangesAsync(ct);

        return new UpdateGeneralDataResponse(
            entity.Id, entity.Group, entity.Category, entity.Name, entity.Description);
    }
}
