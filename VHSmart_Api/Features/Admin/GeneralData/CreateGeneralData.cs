using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.GeneralData;

// Add a dropdown value (spec 5.1, Add button). CompanyId comes from the JWT only (CodingRules
// 8.1); duplicate rule (spec 21.9): the same Name within Company + Group + Category is refused
// while the row is live.
public record CreateGeneralDataCommand(
    GeneralDataGroup? Group,
    string Category,
    string Name,
    string? Description) : IRequest<CreateGeneralDataResponse>;

public record CreateGeneralDataResponse(
    Guid Id,
    GeneralDataGroup Group,
    string Category,
    string Name,
    string? Description);

public class CreateGeneralDataValidator : AbstractValidator<CreateGeneralDataCommand>
{
    public CreateGeneralDataValidator()
    {
        // Group is a nullable enum so a missing value fails validation instead of silently
        // defaulting to the first member (COMPANY).
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

public class CreateGeneralDataHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateGeneralDataCommand, CreateGeneralDataResponse>
{
    public async Task<CreateGeneralDataResponse> Handle(
        CreateGeneralDataCommand request,
        CancellationToken ct)
    {
        if (request.Group is not { } group)
            throw new BusinessRuleException("Group is required.");

        var companyId = user.CompanyId;

        var duplicate = await db.GeneralData.AnyAsync(row =>
            row.CompanyId == companyId
            && row.Group == group
            && row.Category == request.Category
            && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException(
                "A record with this name already exists in the selected group and category.");

        var entity = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = group,
            Category = request.Category,
            Name = request.Name,
            Description = request.Description
        };
        db.GeneralData.Add(entity);
        await db.SaveChangesAsync(ct);

        return new CreateGeneralDataResponse(
            entity.Id, entity.Group, entity.Category, entity.Name, entity.Description);
    }
}
