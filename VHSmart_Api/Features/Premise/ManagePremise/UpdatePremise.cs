using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Edit premise (spec 7.7 modal "Edit Premise", Save): the Premise Information fields plus
// the two child tables of the modal - "Contact Person" (tab Staff Information) and
// "Hostel Information" (tab Facility Information), reconciled the same way as the training
// attendance list (CodingRules 7.1): rows no longer wanted are soft-deleted, new ones are
// added, kept rows stay untouched. The id comes from the route, the company from the JWT;
// the handler loads the premise with an explicit CompanyId match, so another tenant's row
// answers 404 (never 403). Unique e-mail / store code exclude self (Database.md 7) -> 409.
// A hostel input carrying an id that does not belong to this premise (stale client state)
// answers 404 "Hostel not found." before anything is saved.
public record UpdatePremiseCommand(
    Guid Id,
    PremiseType? PremiseType,
    string Name,
    string Email,
    string? StoreCode,
    Guid? PremiseManagerStaffId,
    string? PremiseManagerName,
    Guid? AreaManagerStaffId,
    Guid? OperationManagerStaffId,
    string? BusinessRegistrationNo,
    string? GoogleMapLink,
    string Address1,
    string Address2,
    string? Address3,
    string Postcode,
    string? City,
    string? District,
    Guid? CountryId,
    string State,
    string Telephone,
    string? Fax,
    DateTime? OpeningDate,
    DateTime? ClosingDate,
    string Status,
    Guid? PrayerRoomAvailabilityId,
    IReadOnlyList<Guid>? ContactStaffIds,
    IReadOnlyList<PremiseHostelInput>? Hostels) : IRequest<PremiseDetailResponse>;

// The modal table row as it is saved: null Id = new hostel, an id must reference one of the
// premise's own rows (reconciled by Id, like the other child lists in this system).
public record PremiseHostelInput(
    Guid? Id,
    string HostelName,
    string? Address,
    DateTime? TenancyExpiryDate,
    string? ContactPerson,
    string? PhoneNo);

public class UpdatePremiseValidator : AbstractValidator<UpdatePremiseCommand>
{
    public UpdatePremiseValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PremiseType)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Premise type is required.")
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithMessage("Premise type is invalid.");
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Premise name is required.")
            .MaximumLength(200).WithMessage("Premise name must be 200 characters or fewer.");
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Premise email is required.")
            .MaximumLength(254).WithMessage("Premise email must be 254 characters or fewer.");
        RuleFor(x => x.StoreCode).MaximumLength(50)
            .WithMessage("Store code must be 50 characters or fewer.");
        RuleFor(x => x.PremiseManagerStaffId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (id, ct) => id is null
                || await db.Staffs.AnyAsync(
                    row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Premise manager not found.");
        RuleFor(x => x.PremiseManagerName).MaximumLength(200)
            .WithMessage("Premise manager name must be 200 characters or fewer.");
        RuleFor(x => x.AreaManagerStaffId)
            .MustAsync(async (id, ct) => id is null
                || await db.Staffs.AnyAsync(
                    row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Area manager not found.");
        RuleFor(x => x.OperationManagerStaffId)
            .MustAsync(async (id, ct) => id is null
                || await db.Staffs.AnyAsync(
                    row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Operation manager not found.");
        RuleFor(x => x.BusinessRegistrationNo).MaximumLength(50)
            .WithMessage("Business registration number must be 50 characters or fewer.");
        RuleFor(x => x.GoogleMapLink).MaximumLength(500)
            .WithMessage("Google Map link must be 500 characters or fewer.");
        RuleFor(x => x.Address1)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address line 1 is required.")
            .MaximumLength(200).WithMessage("Address line 1 must be 200 characters or fewer.");
        RuleFor(x => x.Address2)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Address line 2 is required.")
            .MaximumLength(200).WithMessage("Address line 2 must be 200 characters or fewer.");
        RuleFor(x => x.Address3).MaximumLength(200)
            .WithMessage("Address line 3 must be 200 characters or fewer.");
        RuleFor(x => x.Postcode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Postcode is required.")
            .MaximumLength(20).WithMessage("Postcode must be 20 characters or fewer.");
        RuleFor(x => x.City).MaximumLength(100)
            .WithMessage("City must be 100 characters or fewer.");
        RuleFor(x => x.District).MaximumLength(100)
            .WithMessage("District must be 100 characters or fewer.");
        RuleFor(x => x.CountryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Country is required.")
            .MustAsync((id, ct) => db.Countries.AnyAsync(row => row.Id == id, ct))
            .WithMessage("Country not found.");
        RuleFor(x => x.State)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100).WithMessage("State must be 100 characters or fewer.");
        RuleFor(x => x.Telephone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Telephone is required.")
            .MaximumLength(30).WithMessage("Telephone must be 30 characters or fewer.");
        RuleFor(x => x.Fax).MaximumLength(30)
            .WithMessage("Fax must be 30 characters or fewer.");
        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .MaximumLength(30).WithMessage("Status must be 30 characters or fewer.");
        RuleFor(x => x.PrayerRoomAvailabilityId)
            .MustAsync(async (id, ct) => id is null
                || await db.GeneralData.AnyAsync(
                    row => row.Id == id
                        && row.CompanyId == user.CompanyId
                        && row.Group == GeneralDataGroup.COMPANY
                        && row.Category == "Prayer Room Availability",
                    ct))
            .WithMessage("Prayer room availability not found.");
        RuleFor(x => x.ContactStaffIds)
            .MustAsync(async (ids, ct) =>
            {
                var distinct = (ids ?? []).Distinct().ToList();
                var found = await db.Staffs.CountAsync(
                    row => row.CompanyId == user.CompanyId && distinct.Contains(row.Id), ct);
                return found == distinct.Count;
            })
            .WithMessage("Contact person not found.");
        RuleForEach(x => x.Hostels).ChildRules(hostel =>
        {
            hostel.RuleFor(row => row.HostelName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Hostel name is required.")
                .MaximumLength(200)
                .WithMessage("Hostel name must be 200 characters or fewer.");
            hostel.RuleFor(row => row.Address).MaximumLength(500)
                .WithMessage("Hostel address must be 500 characters or fewer.");
            hostel.RuleFor(row => row.ContactPerson).MaximumLength(200)
                .WithMessage("Hostel contact person must be 200 characters or fewer.");
            hostel.RuleFor(row => row.PhoneNo).MaximumLength(30)
                .WithMessage("Hostel phone number must be 30 characters or fewer.");
        });
    }
}

public class UpdatePremiseHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdatePremiseCommand, PremiseDetailResponse>
{
    public async Task<PremiseDetailResponse> Handle(
        UpdatePremiseCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var entity = await db.Premises
            .FirstOrDefaultAsync(row => row.Id == request.Id && row.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Premise not found.");

        var duplicateEmail = await db.Premises.AnyAsync(
            row => row.CompanyId == companyId
                && row.Email == request.Email
                && row.Id != request.Id, ct);
        if (duplicateEmail)
            throw new ConflictException("A premise with this e-mail already exists.");

        var storeCode = string.IsNullOrWhiteSpace(request.StoreCode) ? null : request.StoreCode;
        if (storeCode is not null)
        {
            var duplicateStoreCode = await db.Premises.AnyAsync(
                row => row.CompanyId == companyId
                    && row.StoreCode == storeCode
                    && row.Id != request.Id, ct);
            if (duplicateStoreCode)
                throw new ConflictException("A premise with this store code already exists.");
        }

        entity.PremiseType = request.PremiseType!.Value;
        entity.Name = request.Name;
        entity.Email = request.Email;
        entity.StoreCode = storeCode;
        entity.PremiseManagerStaffId = request.PremiseManagerStaffId;
        entity.PremiseManagerName = request.PremiseManagerName;
        entity.AreaManagerStaffId = request.AreaManagerStaffId;
        entity.OperationManagerStaffId = request.OperationManagerStaffId;
        entity.BusinessRegistrationNo = request.BusinessRegistrationNo;
        entity.GoogleMapLink = request.GoogleMapLink;
        entity.Address1 = request.Address1;
        entity.Address2 = request.Address2;
        entity.Address3 = request.Address3;
        entity.Postcode = request.Postcode;
        entity.City = request.City;
        entity.District = request.District;
        entity.CountryId = request.CountryId!.Value;
        entity.State = request.State;
        entity.Telephone = request.Telephone;
        entity.Fax = request.Fax;
        entity.OpeningDate = request.OpeningDate;
        entity.ClosingDate = request.ClosingDate;
        entity.Status = request.Status;
        entity.PrayerRoomAvailabilityId = request.PrayerRoomAvailabilityId;

        // Contact Person list: soft-delete rows for staff no longer picked (the filtered UQ
        // (PremiseId, StaffId) frees the pair), add the new ones, keep the rest.
        var wanted = (request.ContactStaffIds ?? []).Distinct().ToHashSet();
        var currentContacts = await db.PremiseContacts
            .Where(row => row.PremiseId == entity.Id)
            .ToListAsync(ct);

        foreach (var row in currentContacts.Where(row => !wanted.Contains(row.StaffId)))
            db.PremiseContacts.Remove(row);

        var present = currentContacts.Select(row => row.StaffId).ToHashSet();
        foreach (var staffId in wanted.Where(id => !present.Contains(id)))
        {
            db.PremiseContacts.Add(new PremiseContactEntity
            {
                CompanyId = companyId,
                PremiseId = entity.Id,
                StaffId = staffId
            });
        }

        // Hostel Information list: reconciled by input Id (null = new). Every referenced id
        // must be one of this premise's own live rows - validate the whole set first so a
        // stale/foreign id answers 404 before any row is touched.
        var hostelInputs = request.Hostels ?? [];
        var currentHostels = await db.PremiseHostels
            .Where(row => row.PremiseId == entity.Id)
            .ToListAsync(ct);
        var hostelById = currentHostels.ToDictionary(row => row.Id);

        foreach (var input in hostelInputs)
        {
            if (input.Id is Guid id && !hostelById.ContainsKey(id))
                throw new NotFoundException("Hostel not found.");
        }

        var wantedHostelIds = hostelInputs
            .Where(input => input.Id is not null)
            .Select(input => input.Id!.Value)
            .ToHashSet();

        foreach (var row in currentHostels.Where(row => !wantedHostelIds.Contains(row.Id)))
            db.PremiseHostels.Remove(row);

        foreach (var input in hostelInputs)
        {
            if (input.Id is Guid id)
            {
                var hostel = hostelById[id];
                hostel.HostelName = input.HostelName;
                hostel.Address = input.Address;
                hostel.TenancyExpiryDate = input.TenancyExpiryDate;
                hostel.ContactPerson = input.ContactPerson;
                hostel.PhoneNo = input.PhoneNo;
            }
            else
            {
                db.PremiseHostels.Add(new PremiseHostelEntity
                {
                    CompanyId = companyId,
                    PremiseId = entity.Id,
                    HostelName = input.HostelName,
                    Address = input.Address,
                    TenancyExpiryDate = input.TenancyExpiryDate,
                    ContactPerson = input.ContactPerson,
                    PhoneNo = input.PhoneNo
                });
            }
        }

        await db.SaveChangesAsync(ct);

        return await PremiseResponseLoader.LoadAsync(db, companyId, entity.Id, ct);
    }
}
