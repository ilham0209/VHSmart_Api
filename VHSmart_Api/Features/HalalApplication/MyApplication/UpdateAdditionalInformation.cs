using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// "Save" of the Additional Information tab (spec 12.5): the checkbox state IS the tab, so
// the command carries the full list and the handler replaces the application's rows -
// unticked options are soft-deleted (never a physical DELETE), new ones are inserted.
// OptionCodes come from the ApplicationData catalog (Database.md 10 examples); the same code
// may exist once per section ("OTHERS" appears in both halves). D-26: submitted applications
// are read-only.
public record UpdateAdditionalInformationCommand(
    Guid Id,
    IReadOnlyList<AdditionalInfoItemInput> Items)
    : IRequest<IReadOnlyList<ApplicationAdditionalInfoItemResponse>>;

public record AdditionalInfoItemInput(
    ApplicationAdditionalInfoSection Section,
    string OptionCode,
    string? FreeText);

public class UpdateAdditionalInformationValidator
    : AbstractValidator<UpdateAdditionalInformationCommand>
{
    public UpdateAdditionalInformationValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Additional information items are required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(row => row.Section)
                .Must(section => Enum.IsDefined(section))
                .WithMessage("Additional information section is not valid.");

            item.RuleFor(row => row.OptionCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Additional information option is required.")
                .MaximumLength(50)
                .WithMessage("Additional information option must be 50 characters or fewer.");

            item.RuleFor(row => row.FreeText).MaximumLength(500)
                .WithMessage("Additional information text must be 500 characters or fewer.");
        });

        RuleFor(x => x.Items)
            .Must(items => items is null || items.All(item =>
                ApplicationData.IsKnownOption(item.Section, item.OptionCode ?? string.Empty)))
            .WithMessage("Additional information option is not valid for its section.");

        RuleFor(x => x.Items)
            .Must(items => items is null
                || items.Select(item => (item.Section, item.OptionCode)).Distinct().Count()
                    == items.Count)
            .WithMessage("Additional information option is duplicated.");
    }
}

public class UpdateAdditionalInformationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateAdditionalInformationCommand,
        IReadOnlyList<ApplicationAdditionalInfoItemResponse>>
{
    public async Task<IReadOnlyList<ApplicationAdditionalInfoItemResponse>> Handle(
        UpdateAdditionalInformationCommand request,
        CancellationToken ct)
    {
        var application = await db.Applications
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Application not found.");

        ApplicationData.EnsureDraft(application);

        var existing = await db.ApplicationAdditionalInfoItems
            .Where(row => row.ApplicationId == application.Id)
            .ToListAsync(ct);

        // Replace-all is a soft delete (CodingRules 7.2 - no physical DELETE in this product).
        foreach (var row in existing)
            row.IsDeleted = true;

        foreach (var item in request.Items)
        {
            db.ApplicationAdditionalInfoItems.Add(new ApplicationAdditionalInfoItemEntity
            {
                CompanyId = application.CompanyId,
                ApplicationId = application.Id,
                Section = item.Section,
                OptionCode = item.OptionCode,
                FreeText = string.IsNullOrWhiteSpace(item.FreeText) ? null : item.FreeText
            });
        }

        await db.SaveChangesAsync(ct);

        return request.Items
            .Select(item => new ApplicationAdditionalInfoItemResponse(
                item.Section,
                item.OptionCode,
                string.IsNullOrWhiteSpace(item.FreeText) ? null : item.FreeText))
            .OrderBy(item => item.Section)
            .ThenBy(item => item.OptionCode, StringComparer.Ordinal)
            .ToList();
    }
}
