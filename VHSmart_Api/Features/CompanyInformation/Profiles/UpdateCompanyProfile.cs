using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Profiles;

// Save of Company > Profiles (spec 7.3): Contact Person, Halal Executive, their working
// hours and No. of Employee. The company id comes from the route because the spec 7.3
// super-user list edits any company's profile - the handler re-checks the scope (platform
// admin, otherwise own company only), so the URL never widens access. The command carries no
// user id or company id of its own: identity always stays in the JWT (CodingRules 7.4).
// One live row per kind is kept (the spec form has exactly one slot of each), so a null staff
// id clears the slot and a different staff id replaces it.
public record UpdateCompanyProfileCommand(
    Guid CompanyId,
    Guid? ContactPersonStaffId,
    TimeOnly? ContactPersonWorkingHourFrom,
    TimeOnly? ContactPersonWorkingHourTo,
    Guid? HalalExecutiveStaffId,
    TimeOnly? HalalExecutiveWorkingHourFrom,
    TimeOnly? HalalExecutiveWorkingHourTo,
    int? NumberOfEmployees) : IRequest<CompanyProfileResponse>;

public class UpdateCompanyProfileValidator : AbstractValidator<UpdateCompanyProfileCommand>
{
    public UpdateCompanyProfileValidator(VHSmartDbContext db)
    {
        // Chosen from All Staff (spec 7.3) and must belong to the profile's company: the
        // tenant filter hides other companies' staff, so a foreign id reads as "not found".
        RuleFor(x => x.ContactPersonStaffId)
            .MustAsync(async (command, staffId, ct) =>
                staffId is null
                || await db.Staffs.AnyAsync(
                    staff => staff.Id == staffId && staff.CompanyId == command.CompanyId, ct))
            .WithMessage("Contact person not found.");
        RuleFor(x => x.HalalExecutiveStaffId)
            .MustAsync(async (command, staffId, ct) =>
                staffId is null
                || await db.Staffs.AnyAsync(
                    staff => staff.Id == staffId && staff.CompanyId == command.CompanyId, ct))
            .WithMessage("Halal executive not found.");
        RuleFor(x => x.NumberOfEmployees)
            .GreaterThanOrEqualTo(0)
            .When(x => x.NumberOfEmployees.HasValue)
            .WithMessage("Number of employees cannot be negative.");
    }
}

public class UpdateCompanyProfileHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCompanyProfileCommand, CompanyProfileResponse>
{
    public async Task<CompanyProfileResponse> Handle(
        UpdateCompanyProfileCommand request,
        CancellationToken ct)
    {
        var company = await CompanyProfileLoader.LoadCompanyAsync(
            db, user, request.CompanyId, ct);

        await ApplyContactAsync(
            db,
            company.Id,
            CompanyContactKind.ContactPerson,
            request.ContactPersonStaffId,
            request.ContactPersonWorkingHourFrom,
            request.ContactPersonWorkingHourTo,
            ct);
        await ApplyContactAsync(
            db,
            company.Id,
            CompanyContactKind.HalalExecutive,
            request.HalalExecutiveStaffId,
            request.HalalExecutiveWorkingHourFrom,
            request.HalalExecutiveWorkingHourTo,
            ct);

        company.NumberOfEmployees = request.NumberOfEmployees;

        await db.SaveChangesAsync(ct);

        return await CompanyProfileResponse.LoadAsync(db, company, ct);
    }

    // Keeps at most one live row per kind: rows for another staff member are soft-deleted so
    // the pair (CompanyId, Kind, StaffId) stays unique (Database.md 4) and a re-pick of the
    // same person only updates the working hours instead of adding a second row.
    private static async Task ApplyContactAsync(
        VHSmartDbContext db,
        Guid companyId,
        CompanyContactKind kind,
        Guid? staffId,
        TimeOnly? workingHourFrom,
        TimeOnly? workingHourTo,
        CancellationToken ct)
    {
        var rows = await db.CompanyContacts
            .Where(contact => contact.CompanyId == companyId && contact.Kind == kind)
            .ToListAsync(ct);

        if (staffId is null)
        {
            foreach (var row in rows)
                db.CompanyContacts.Remove(row);
            return;
        }

        foreach (var row in rows.Where(contact => contact.StaffId != staffId))
            db.CompanyContacts.Remove(row);

        var current = rows.FirstOrDefault(contact => contact.StaffId == staffId);
        if (current is null)
        {
            db.CompanyContacts.Add(new CompanyContactEntity
            {
                CompanyId = companyId,
                Kind = kind,
                StaffId = staffId.Value,
                WorkingHourFrom = workingHourFrom,
                WorkingHourTo = workingHourTo
            });
            return;
        }

        current.WorkingHourFrom = workingHourFrom;
        current.WorkingHourTo = workingHourTo;
    }
}
