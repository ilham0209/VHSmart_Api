using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Add premise (spec 7.7 modal "Premise Information", "one at a time via the form"): the
// premise fields only - the Contact Person and Hostel tables belong to the edit modal and
// are reconciled by the update command. PremiseType/Status travel so that a missing value
// fails with "required" instead of silently defaulting (PremiseType is a nullable enum, the
// R-02 pattern; Status is a plain required string - the spec shows "e.g. ACTIVE" but marks
// it required, so no value is defaulted). For Company* is the caller's own company and never
// part of the command (CodingRules 8.1). Server-enforced uniqueness (spec 7.7 [CODE] and
// Database.md 7): premise e-mail and store code are unique per company among live rows ->
// 409. StoreCode/Brand are settable here even though the confirmed form screenshot does not
// show an input for them (the TaskList row names "unique e-mail/store code" as PR-01 scope;
// Brand is NOT writable - no confirmed editor exists, see report). Manager/country values
// are validated as live rows so a stale dropdown answers 400 instead of an FK error (same
// as CreateCompany). Opening/Closing Date and the two-address-line rule have no cross
// validation in the spec (nothing exists), so only lengths are enforced.
public record CreatePremiseCommand(
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
    Guid? PrayerRoomAvailabilityId) : IRequest<PremiseDetailResponse>;

public class CreatePremiseValidator : AbstractValidator<CreatePremiseCommand>
{
    public CreatePremiseValidator(VHSmartDbContext db, ICurrentUser user)
    {
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
    }
}

public class CreatePremiseHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreatePremiseCommand, PremiseDetailResponse>
{
    public async Task<PremiseDetailResponse> Handle(
        CreatePremiseCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var duplicateEmail = await db.Premises.AnyAsync(
            row => row.CompanyId == companyId && row.Email == request.Email, ct);
        if (duplicateEmail)
            throw new ConflictException("A premise with this e-mail already exists.");

        var storeCode = string.IsNullOrWhiteSpace(request.StoreCode) ? null : request.StoreCode;
        if (storeCode is not null)
        {
            var duplicateStoreCode = await db.Premises.AnyAsync(
                row => row.CompanyId == companyId && row.StoreCode == storeCode, ct);
            if (duplicateStoreCode)
                throw new ConflictException("A premise with this store code already exists.");
        }

        var entity = new PremiseEntity
        {
            CompanyId = companyId,
            PremiseType = request.PremiseType!.Value,
            Name = request.Name,
            Email = request.Email,
            StoreCode = storeCode,
            PremiseManagerStaffId = request.PremiseManagerStaffId,
            PremiseManagerName = request.PremiseManagerName,
            AreaManagerStaffId = request.AreaManagerStaffId,
            OperationManagerStaffId = request.OperationManagerStaffId,
            BusinessRegistrationNo = request.BusinessRegistrationNo,
            GoogleMapLink = request.GoogleMapLink,
            Address1 = request.Address1,
            Address2 = request.Address2,
            Address3 = request.Address3,
            Postcode = request.Postcode,
            City = request.City,
            District = request.District,
            CountryId = request.CountryId!.Value,
            State = request.State,
            Telephone = request.Telephone,
            Fax = request.Fax,
            OpeningDate = request.OpeningDate,
            ClosingDate = request.ClosingDate,
            Status = request.Status,
            PrayerRoomAvailabilityId = request.PrayerRoomAvailabilityId
        };
        db.Premises.Add(entity);

        await db.SaveChangesAsync(ct);

        return await PremiseResponseLoader.LoadAsync(db, companyId, entity.Id, ct);
    }
}
