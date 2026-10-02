using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Profiles;

// Company > Profiles detail (spec 7.3): the Contact Person / Halal Executive slots plus the
// No. of Employee field. Both people are chosen from All Staff, so the response carries the
// staff details the form shows (name, designation, e-mail) next to the stored working hours.
public record GetCompanyProfileQuery(Guid CompanyId) : IRequest<CompanyProfileResponse>;

public record ProfileContactResponse(
    Guid? StaffId,
    string? Name,
    string? Designation,
    string? Email,
    TimeOnly? WorkingHourFrom,
    TimeOnly? WorkingHourTo);

public record CompanyProfileResponse(
    Guid CompanyId,
    string CompanyName,
    string? CertificationBodyName,
    string BusinessRegistrationNo,
    int? NumberOfEmployees,
    ProfileContactResponse? ContactPerson,
    ProfileContactResponse? HalalExecutive,
    DateTime? ModifiedDate)
{
    internal static async Task<CompanyProfileResponse> LoadAsync(
        VHSmartDbContext db,
        CompanyEntity company,
        CancellationToken ct)
    {
        var certificationBodyName = await db.CertificationBodies
            .AsNoTracking()
            .Where(body => body.Id == company.CertificationBodyId)
            .Select(body => body.Name)
            .FirstOrDefaultAsync(ct);

        var contacts = await db.CompanyContacts
            .AsNoTracking()
            .Where(contact => contact.CompanyId == company.Id)
            .Select(contact => new ContactRow(
                contact.Kind,
                contact.StaffId,
                contact.Staff == null ? null : contact.Staff.Name,
                contact.Staff == null || contact.Staff.Designation == null
                    ? null
                    : contact.Staff.Designation.Name,
                contact.Staff == null ? null : contact.Staff.Email,
                contact.WorkingHourFrom,
                contact.WorkingHourTo))
            .ToListAsync(ct);

        var contactPerson = contacts.FirstOrDefault(row => row.Kind == CompanyContactKind.ContactPerson);
        var halalExecutive = contacts.FirstOrDefault(row => row.Kind == CompanyContactKind.HalalExecutive);

        return new CompanyProfileResponse(
            company.Id,
            company.Name,
            certificationBodyName,
            company.BusinessRegistrationNo,
            company.NumberOfEmployees,
            contactPerson is null ? null : From(contactPerson),
            halalExecutive is null ? null : From(halalExecutive),
            company.SysDateModified);
    }

    private static ProfileContactResponse From(ContactRow row) =>
        new(
            row.StaffId,
            row.Name,
            row.Designation,
            row.Email,
            row.WorkingHourFrom,
            row.WorkingHourTo);

    private sealed record ContactRow(
        CompanyContactKind Kind,
        Guid StaffId,
        string? Name,
        string? Designation,
        string? Email,
        TimeOnly? WorkingHourFrom,
        TimeOnly? WorkingHourTo);
}

public class GetCompanyProfileHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCompanyProfileQuery, CompanyProfileResponse>
{
    public async Task<CompanyProfileResponse> Handle(
        GetCompanyProfileQuery request,
        CancellationToken ct)
    {
        var company = await CompanyProfileLoader.LoadCompanyAsync(
            db, user, request.CompanyId, ct);
        return await CompanyProfileResponse.LoadAsync(db, company, ct);
    }
}

internal static class CompanyProfileLoader
{
    // The profile is addressed by company id in the URL because the spec 7.3 super-user list
    // opens any company's profile (spec 7.3), so scope cannot come from the URL alone: a
    // platform admin may load any company, everyone else only the company their token
    // carries. Out of scope answers 404 - never 403 - so the API does not reveal that the
    // company exists (CodingRules 9).
    internal static async Task<CompanyEntity> LoadCompanyAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        Guid companyId,
        CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(
            row => row.Id == companyId, ct);

        if (company is null || (!user.IsPlatformAdmin && company.Id != user.CompanyId))
            throw new NotFoundException("Company not found.");

        return company;
    }
}
