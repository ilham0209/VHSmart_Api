using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Detail of one premise - the state the "View Premise / Edit Premise" modal opens with
// (spec 7.7): the Premise Information fields, the Contact Person table (tab "Staff
// Information") and the Hostel Information table (tab "Facility Information"). The Premise
// Attachment, Product/Menu (Current/Approved) and Halal Information tabs are NOT here -
// TaskList puts them in PR-02 (attachments) and PR-04 (halal/menu read-only joins).
// The PremiseManager display resolves the staff row when one was picked ("Yes") and falls
// back to the typed name ("No"); PremiseManagerStaffId/PremiseManagerName stay raw so the
// edit form round-trips both branches. An unknown or foreign premise answers 404 (never
// 403). The same loader builds the response after Create/Update, so one code path defines
// the shape (same pattern as the training loader).
public record GetPremiseByIdQuery(Guid Id) : IRequest<PremiseDetailResponse>;

public record PremiseDetailResponse(
    Guid Id,
    string Company,
    PremiseType PremiseType,
    string Name,
    string Email,
    string? StoreCode,
    Guid? PremiseManagerStaffId,
    string? PremiseManagerName,
    string PremiseManager,
    Guid? AreaManagerStaffId,
    string AreaManager,
    Guid? OperationManagerStaffId,
    string OperationManager,
    string? BusinessRegistrationNo,
    string? GoogleMapLink,
    string Address1,
    string Address2,
    string? Address3,
    string Postcode,
    string? City,
    string? District,
    Guid CountryId,
    string Country,
    string State,
    string Telephone,
    string? Fax,
    DateTime? OpeningDate,
    DateTime? ClosingDate,
    string Status,
    Guid? BrandId,
    string Brand,
    Guid? PrayerRoomAvailabilityId,
    string PrayerRoomAvailability,
    IReadOnlyList<PremiseContactResponse> Contacts,
    IReadOnlyList<PremiseHostelResponse> Hostels);

public record PremiseContactResponse(
    Guid StaffId,
    int No,
    string Name,
    string Designation,
    string Email,
    string? MobileNumber);

public record PremiseHostelResponse(
    Guid Id,
    int No,
    string HostelName,
    string? Address,
    DateTime? TenancyExpiryDate,
    string? ContactPerson,
    string? PhoneNo);

public class GetPremiseByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPremiseByIdQuery, PremiseDetailResponse>
{
    public Task<PremiseDetailResponse> Handle(
        GetPremiseByIdQuery request,
        CancellationToken ct) =>
        PremiseResponseLoader.LoadAsync(db, user.CompanyId, request.Id, ct);
}

internal static class PremiseResponseLoader
{
    public static async Task<PremiseDetailResponse> LoadAsync(
        VHSmartDbContext db,
        Guid companyId,
        Guid premiseId,
        CancellationToken ct)
    {
        var premise = await db.Premises
            .AsNoTracking()
            .Where(row => row.Id == premiseId && row.CompanyId == companyId)
            .Select(row => new
            {
                row.Id,
                row.PremiseType,
                row.Name,
                row.Email,
                row.StoreCode,
                row.PremiseManagerStaffId,
                row.PremiseManagerName,
                row.AreaManagerStaffId,
                row.OperationManagerStaffId,
                row.BusinessRegistrationNo,
                row.GoogleMapLink,
                row.Address1,
                row.Address2,
                row.Address3,
                row.Postcode,
                row.City,
                row.District,
                row.CountryId,
                row.State,
                row.Telephone,
                row.Fax,
                row.OpeningDate,
                row.ClosingDate,
                row.Status,
                row.BrandId,
                row.PrayerRoomAvailabilityId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Premise not found.");

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(row => row.Id == companyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var countryName = await db.Countries
            .AsNoTracking()
            .Where(row => row.Id == premise.CountryId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        // Soft-deleted staff drop out of the global filter, so a stale id resolves to "".
        var areaManagerName = await db.Staffs
            .AsNoTracking()
            .Where(row => row.Id == premise.AreaManagerStaffId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var operationManagerName = await db.Staffs
            .AsNoTracking()
            .Where(row => row.Id == premise.OperationManagerStaffId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var premiseManager = premise.PremiseManagerStaffId is not null
            ? await db.Staffs
                .AsNoTracking()
                .Where(row => row.Id == premise.PremiseManagerStaffId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct) ?? string.Empty
            : premise.PremiseManagerName ?? string.Empty;

        var brandName = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.Id == premise.BrandId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var prayerRoomAvailabilityName = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.Id == premise.PrayerRoomAvailabilityId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var contacts = await (
                from contact in db.PremiseContacts.AsNoTracking()
                    .Where(row => row.PremiseId == premiseId)
                join staff in db.Staffs.AsNoTracking()
                    on contact.StaffId equals staff.Id
                orderby staff.Name
                select new
                {
                    staff.Id,
                    staff.Name,
                    staff.Email,
                    staff.MobileNumber,
                    Designation = staff.Designation == null ? string.Empty : staff.Designation.Name
                })
            .ToListAsync(ct);

        var hostels = await db.PremiseHostels
            .AsNoTracking()
            .Where(row => row.PremiseId == premiseId)
            .OrderBy(row => row.SysDateCreated)
            .ThenBy(row => row.Id)
            .Select(row => new
            {
                row.Id,
                row.HostelName,
                row.Address,
                row.TenancyExpiryDate,
                row.ContactPerson,
                row.PhoneNo
            })
            .ToListAsync(ct);

        return new PremiseDetailResponse(
            premise.Id,
            companyName,
            premise.PremiseType,
            premise.Name,
            premise.Email,
            premise.StoreCode,
            premise.PremiseManagerStaffId,
            premise.PremiseManagerName,
            premiseManager,
            premise.AreaManagerStaffId,
            areaManagerName,
            premise.OperationManagerStaffId,
            operationManagerName,
            premise.BusinessRegistrationNo,
            premise.GoogleMapLink,
            premise.Address1,
            premise.Address2,
            premise.Address3,
            premise.Postcode,
            premise.City,
            premise.District,
            premise.CountryId,
            countryName,
            premise.State,
            premise.Telephone,
            premise.Fax,
            premise.OpeningDate,
            premise.ClosingDate,
            premise.Status,
            premise.BrandId,
            brandName,
            premise.PrayerRoomAvailabilityId,
            prayerRoomAvailabilityName,
            [.. contacts.Select((row, index) => new PremiseContactResponse(
                row.Id,
                index + 1,
                row.Name,
                row.Designation,
                row.Email,
                row.MobileNumber))],
            [.. hostels.Select((row, index) => new PremiseHostelResponse(
                row.Id,
                index + 1,
                row.HostelName,
                row.Address,
                row.TenancyExpiryDate,
                row.ContactPerson,
                row.PhoneNo))]);
    }
}
