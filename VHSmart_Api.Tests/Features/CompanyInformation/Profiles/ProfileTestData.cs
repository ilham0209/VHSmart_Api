using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Tests.Features.Admin.Companies;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

// Fixtures for Company > Profiles: companies, staff and the PEOPLE dropdown rows behind them,
// plus one valid-command builder (the record has 8 fields, so a builder keeps the test files
// honest). Company ids are chosen by the test, because every scenario here is about which
// company the id points at.
internal static class ProfileTestData
{
    public static TestCurrentUser CompanyUser(
        Guid? companyId = null,
        bool viewAllCompanies = false) =>
        new(
            Guid.NewGuid().ToString(),
            companyId ?? Guid.NewGuid(),
            viewAllCompanies: viewAllCompanies);

    public static TestCurrentUser PlatformUser() => CompanyTestData.PlatformUser();

    public static Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user) =>
        CompanyTestData.CreateDbAsync(user);

    // Seeds a company (with its own certification body, so the list can show the CB name).
    public static async Task<Guid> SeedCompanyAsync(
        TestableVHSmartDbContext db,
        string name = "Acme Foods",
        Guid? companyId = null)
    {
        var row = new CompanyEntity
        {
            Id = companyId ?? Guid.NewGuid(),
            Name = name,
            CertificationBodyId = await CompanyTestData.SeedCertificationBodyAsync(db, $"{name} CB"),
            RegistrationType = "Companies Commission Of Malaysia",
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            OwnerStatus = "Muslim Owner",
            Address1 = "Jalan Perusahaan 1",
            Address2 = "Jalan Perusahaan 2",
            PostCode = "50000",
            District = "Gombak",
            State = "Selangor",
            CountryId = await CompanyTestData.MalaysiaIdAsync(db),
            Telephone = "0300000000",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Companies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<string> BusinessRegistrationNoAsync(
        TestableVHSmartDbContext db,
        Guid companyId) =>
        await db.Companies
            .AsNoTracking()
            .Where(row => row.Id == companyId)
            .Select(row => row.BusinessRegistrationNo)
            .SingleAsync();

    // A staff row with valid PEOPLE FKs (spec 7.4 lists); the designation name is what the
    // profile response shows next to the person.
    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Aiman Rahman",
        string designation = "Halal Executive")
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.com",
            TitleId = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr"),
            DepartmentId = await AddPeopleValueAsync(db, companyId, "Department", "Production"),
            DesignationId = await AddPeopleValueAsync(db, companyId, "Designation", designation)
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Inserts profile rows directly, so a read test does not have to go through the save path.
    public static async Task SeedContactAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        CompanyContactKind kind,
        Guid staffId,
        TimeOnly? workingHourFrom = null,
        TimeOnly? workingHourTo = null)
    {
        db.CompanyContacts.Add(new CompanyContactEntity
        {
            CompanyId = companyId,
            Kind = kind,
            StaffId = staffId,
            WorkingHourFrom = workingHourFrom,
            WorkingHourTo = workingHourTo
        });
        await db.SaveChangesAsync();
    }

    public static UpdateCompanyProfileCommand ValidCommand(
        Guid companyId,
        Guid? contactPersonStaffId = null,
        Guid? halalExecutiveStaffId = null,
        int? numberOfEmployees = 42) =>
        new(
            companyId,
            contactPersonStaffId,
            new TimeOnly(9, 0),
            new TimeOnly(17, 30),
            halalExecutiveStaffId,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            numberOfEmployees);

    private static async Task<Guid> AddPeopleValueAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string category,
        string name)
    {
        var existing = await db.GeneralData
            .FirstOrDefaultAsync(row =>
                row.CompanyId == companyId && row.Category == category && row.Name == name);
        if (existing is not null)
            return existing.Id;

        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PEOPLE,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
