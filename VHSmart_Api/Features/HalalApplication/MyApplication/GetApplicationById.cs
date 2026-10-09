using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// The application screen (spec 12.5): header block + the tab payloads the screen opens on.
// General Information is the survey answers (the question texts come from the
// survey-questions endpoint); Company Information carries the read-only pre-fill from the
// company profile, the application extras of the save button and the three read-only people
// sections; Additional Information is the stored checkbox rows. The Establishment / Product
// / Raw Material tabs read from the batch and have their own endpoints (they are lists, not
// form state). Unknown or foreign application -> 404. The company block is read live from
// ComCompanies by the application's own CompanyId - Database.md 10: company info is read,
// never copied.
public record GetApplicationByIdQuery(Guid Id) : IRequest<ApplicationDetailResponse>;

public record ApplicationDetailResponse(
    Guid Id,
    string ReferenceNo,
    string Status,
    DateTime StatusDate,
    string ApplicationType,
    string CompanyName,
    string? CbApplicationNo,
    DateOnly? CbApplicationDate,
    string? HalalCoachName,
    Guid? BatchId,
    string? BatchName,
    string CreatedBy,
    DateTime CreatedDate,
    string? LastModifiedBy,
    DateTime? LastModifiedDate,
    ApplicationSurveyResponse Survey,
    ApplicationCompanySectionResponse Company,
    ApplicationCompanyExtrasResponse Extras,
    ApplicationContactResponse? ContactPerson,
    ApplicationContactResponse? HalalExecutive,
    IReadOnlyList<ApplicationIhcMemberResponse> IhcMembers,
    IReadOnlyList<ApplicationAdditionalInfoItemResponse> AdditionalInformation);

public record ApplicationSurveyResponse(
    bool ReadProcedureManual,
    bool ReadMs1500,
    bool HandlesProhibited,
    bool HasIhc);

// Read-only pre-fill of the Company Information tab (spec 12.5): every field listed there
// comes from ComCompanies / AdmCertificationBodies live.
public record ApplicationCompanySectionResponse(
    Guid CompanyId,
    string CompanyName,
    ApplicationCertificationBodyResponse CertificationBody,
    string? RegistrationType,
    string BusinessRegistrationNo,
    string? OwnerStatus,
    string Address1,
    string Address2,
    string? Address3,
    string PostCode,
    string? City,
    string? State,
    string Telephone,
    string? Fax,
    string SchemeName,
    string? IndustrySize,
    string? WebsiteUrl,
    string Email);

public record ApplicationCertificationBodyResponse(
    string Name,
    string? Address,
    string? Telephone,
    string? Email);

// The saveable application fields of the same tab (spec 12.5 "Extra application fields" and
// the editable employee counts).
public record ApplicationCompanyExtrasResponse(
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
    int? NonMuslimChef);

public record ApplicationContactResponse(
    string? Name,
    string? Designation,
    string? Email,
    string? OfficeNumber,
    TimeOnly? WorkingHourFrom,
    TimeOnly? WorkingHourTo);

public record ApplicationIhcMemberResponse(
    string Name,
    string? Designation,
    string? IhcRole,
    string? Email,
    string? OfficeNumber);

public record ApplicationAdditionalInfoItemResponse(
    ApplicationAdditionalInfoSection Section,
    string OptionCode,
    string? FreeText);

public class GetApplicationByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetApplicationByIdQuery, ApplicationDetailResponse>
{
    public async Task<ApplicationDetailResponse> Handle(
        GetApplicationByIdQuery request,
        CancellationToken ct)
    {
        var application = await ApplicationData.FindAsync(db, user, request.Id, ct);

        var batchName = application.BatchId is null
            ? null
            : await db.Batches.AsNoTracking()
                .Where(row => row.Id == application.BatchId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct);

        var company = await db.Companies.AsNoTracking()
            .Where(row => row.Id == application.CompanyId)
            .Select(row => new
            {
                row.Id,
                row.Name,
                row.CertificationBodyId,
                row.RegistrationType,
                row.BusinessRegistrationNo,
                row.OwnerStatus,
                row.Address1,
                row.Address2,
                row.Address3,
                row.PostCode,
                row.City,
                row.State,
                row.Telephone,
                row.Fax,
                row.SchemeId,
                row.IndustrySize,
                row.WebsiteUrl,
                row.Email
            })
            .FirstOrDefaultAsync(ct);

        ApplicationCompanySectionResponse companySection;
        if (company is null)
        {
            // The tenant root row should always exist; the screen still renders empty.
            companySection = new ApplicationCompanySectionResponse(
                application.CompanyId,
                string.Empty,
                new ApplicationCertificationBodyResponse(string.Empty, null, null, null),
                null, string.Empty, null, string.Empty, string.Empty, null, string.Empty,
                null, null, string.Empty, null, string.Empty, null, null, string.Empty);
        }
        else
        {
            var certificationBody = await db.CertificationBodies.AsNoTracking()
                .Where(row => row.Id == company.CertificationBodyId)
                .Select(row => new
                {
                    row.Name,
                    Address = ((row.Address1 ?? string.Empty) + " "
                        + (row.City ?? string.Empty)).Trim(),
                    row.Telephone,
                    row.Email
                })
                .FirstOrDefaultAsync(ct);

            var schemeName = company.SchemeId is null
                ? string.Empty
                : await db.Schemes.AsNoTracking()
                    .Where(row => row.Id == company.SchemeId)
                    .Select(row => row.Name)
                    .FirstOrDefaultAsync(ct) ?? string.Empty;

            companySection = new ApplicationCompanySectionResponse(
                company.Id,
                company.Name,
                new ApplicationCertificationBodyResponse(
                    certificationBody?.Name ?? string.Empty,
                    string.IsNullOrWhiteSpace(certificationBody?.Address)
                        ? null
                        : certificationBody.Address,
                    certificationBody?.Telephone,
                    certificationBody?.Email),
                company.RegistrationType,
                company.BusinessRegistrationNo,
                company.OwnerStatus,
                company.Address1,
                company.Address2,
                company.Address3,
                company.PostCode,
                company.City,
                company.State,
                company.Telephone,
                company.Fax,
                schemeName,
                company.IndustrySize,
                company.WebsiteUrl,
                company.Email);
        }

        var contactPeople = await (
                from contact in db.CompanyContacts.AsNoTracking()
                where contact.CompanyId == application.CompanyId
                join staff in db.Staffs.AsNoTracking()
                    on contact.StaffId equals staff.Id
                select new
                {
                    contact.Kind,
                    contact.WorkingHourFrom,
                    contact.WorkingHourTo,
                    staff.Name,
                    Designation = staff.Designation == null ? null : staff.Designation.Name,
                    staff.Email,
                    staff.OfficeNumber
                })
            .ToListAsync(ct);

        var contactPerson = contactPeople
            .FirstOrDefault(row => row.Kind == CompanyContactKind.ContactPerson);
        var halalExecutive = contactPeople
            .FirstOrDefault(row => row.Kind == CompanyContactKind.HalalExecutive);

        // IHC is computed from staff flagged as members (Database.md 7 note - no table), the
        // same roster Company > IHC shows: "members with a role" (a flagged row without a
        // role is the data hole spec 7.6 already keeps off the chart), ordered by role then
        // name like the organisation chart.
        var ihcMembers = await (
                from staff in db.Staffs.AsNoTracking()
                where staff.CompanyId == application.CompanyId
                    && staff.IsIhcMember
                    && staff.IhcRoleId != null
                orderby staff.IhcRole!.Name, staff.Name
                select new ApplicationIhcMemberResponse(
                    staff.Name,
                    staff.Designation == null ? null : staff.Designation.Name,
                    staff.IhcRole == null ? null : staff.IhcRole.Name,
                    staff.Email,
                    staff.OfficeNumber))
            .ToListAsync(ct);

        var additionalInformation = await db.ApplicationAdditionalInfoItems.AsNoTracking()
            .Where(row => row.ApplicationId == application.Id)
            .OrderBy(row => row.Section)
            .ThenBy(row => row.OptionCode)
            .Select(row => new ApplicationAdditionalInfoItemResponse(
                row.Section, row.OptionCode, row.FreeText))
            .ToListAsync(ct);

        return new ApplicationDetailResponse(
            application.Id,
            application.ReferenceNo,
            application.Status,
            application.StatusDate,
            application.ApplicationType,
            companySection.CompanyName,
            application.CbApplicationNo,
            application.CbApplicationDate,
            application.HalalCoachName,
            application.BatchId,
            batchName,
            application.SysUserCreated,
            application.SysDateCreated,
            application.SysUserModified,
            application.SysDateModified,
            new ApplicationSurveyResponse(
                application.SurveyReadProcedureManual,
                application.SurveyReadMs1500,
                application.SurveyHandlesProhibited,
                application.SurveyHasIhc),
            companySection,
            new ApplicationCompanyExtrasResponse(
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
                application.NonMuslimChef),
            contactPerson is null
                ? null
                : new ApplicationContactResponse(
                    contactPerson.Name,
                    contactPerson.Designation,
                    contactPerson.Email,
                    contactPerson.OfficeNumber,
                    contactPerson.WorkingHourFrom,
                    contactPerson.WorkingHourTo),
            halalExecutive is null
                ? null
                : new ApplicationContactResponse(
                    halalExecutive.Name,
                    halalExecutive.Designation,
                    halalExecutive.Email,
                    halalExecutive.OfficeNumber,
                    halalExecutive.WorkingHourFrom,
                    halalExecutive.WorkingHourTo),
            ihcMembers,
            additionalInformation);
    }
}
