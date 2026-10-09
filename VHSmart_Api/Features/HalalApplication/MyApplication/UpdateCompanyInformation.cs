using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// "Save" of the Company Information tab (spec 12.5): the application's own extra fields
// (Yearly Sales Revenue, Product Market, Working Hour, Number of Shifts) plus the editable
// employee counts by religion and role. The read-only half of the tab (company registration
// details, IHC, contact person, halal executive) is never written here - those come from
// Company > General / Profiles / All Staff. D-26: submitted applications are read-only.
public record UpdateCompanyInformationCommand(
    Guid Id,
    string? YearlySalesRevenue,
    string? ProductMarket,
    TimeOnly? WorkingHourFrom,
    TimeOnly? WorkingHourTo,
    int? NumberOfShifts,
    int? MuslimManagement,
    int? MuslimFoodHandler,
    int? MuslimChef,
    int? NonMuslimManagement,
    int? NonMuslimFoodHandler,
    int? NonMuslimChef) : IRequest<ApplicationCompanyExtrasResponse>;

public class UpdateCompanyInformationValidator
    : AbstractValidator<UpdateCompanyInformationCommand>
{
    public UpdateCompanyInformationValidator()
    {
        RuleFor(x => x.YearlySalesRevenue).MaximumLength(100)
            .WithMessage("Yearly sales revenue must be 100 characters or fewer.");

        RuleFor(x => x.ProductMarket)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "Domestic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "International", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Product market must be Domestic or International.");

        // One rule for the seven counters - negative people or shifts are never valid.
        RuleFor(x => x)
            .Must(x => x.NumberOfShifts is null or >= 0
                && x.MuslimManagement is null or >= 0
                && x.MuslimFoodHandler is null or >= 0
                && x.MuslimChef is null or >= 0
                && x.NonMuslimManagement is null or >= 0
                && x.NonMuslimFoodHandler is null or >= 0
                && x.NonMuslimChef is null or >= 0)
            .WithMessage(
                "Employee counts and number of shifts must be 0 or more.");
    }
}

public class UpdateCompanyInformationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCompanyInformationCommand, ApplicationCompanyExtrasResponse>
{
    public async Task<ApplicationCompanyExtrasResponse> Handle(
        UpdateCompanyInformationCommand request,
        CancellationToken ct)
    {
        var application = await db.Applications
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Application not found.");

        ApplicationData.EnsureDraft(application);

        application.YearlySalesRevenue = request.YearlySalesRevenue;
        application.ProductMarket = string.IsNullOrWhiteSpace(request.ProductMarket)
            ? null
            : NormalizeProductMarket(request.ProductMarket);
        application.WorkingHourFrom = request.WorkingHourFrom;
        application.WorkingHourTo = request.WorkingHourTo;
        application.NumberOfShifts = request.NumberOfShifts;
        application.MuslimManagement = request.MuslimManagement;
        application.MuslimFoodHandler = request.MuslimFoodHandler;
        application.MuslimChef = request.MuslimChef;
        application.NonMuslimManagement = request.NonMuslimManagement;
        application.NonMuslimFoodHandler = request.NonMuslimFoodHandler;
        application.NonMuslimChef = request.NonMuslimChef;
        await db.SaveChangesAsync(ct);

        return new ApplicationCompanyExtrasResponse(
            application.YearlySalesRevenue,
            application.ProductMarket,
            application.WorkingHourFrom,
            application.WorkingHourTo,
            application.NumberOfShifts,
            application.MuslimManagement,
            application.MuslimFoodHandler,
            application.MuslimChef,
            application.NonMuslimManagement,
            application.NonMuslimFoodHandler,
            application.NonMuslimChef);
    }

    private static string NormalizeProductMarket(string value) =>
        string.Equals(value, "International", StringComparison.OrdinalIgnoreCase)
            ? "International"
            : "Domestic";
}
