using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

// Fixtures for Company > Internal Halal Committee: companies, IHC staff, minutes meetings and
// the create/update command builders. Company ids are chosen by the test, because every
// scenario here is about which company the id points at.
internal static class MinutesMeetingTestData
{
    public static TestCurrentUser CompanyUser(Guid? companyId = null) =>
        new(Guid.NewGuid().ToString(), companyId ?? Guid.NewGuid());

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    // The company row behind a Company cell (the list joins ComCompanies for the name).
    public static async Task<Guid> SeedCompanyAsync(
        TestableVHSmartDbContext db,
        string name = "Verify Halal Sdn Bhd",
        Guid? companyId = null)
    {
        var row = new CompanyEntity
        {
            Id = companyId ?? Guid.NewGuid(),
            Name = name,
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            Address1 = "1 Jalan Verify",
            Address2 = string.Empty,
            PostCode = "50000",
            District = "Kuala Lumpur",
            State = "Kuala Lumpur",
            Telephone = "0312345678",
            Email = $"{Guid.NewGuid():N}@verify.my"
        };
        db.Companies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A staff row with valid PEOPLE FKs. ihcMember with a null ihcRole models the spec's
    // "IHC members with a role" filter (7.6): flagged members without a role exist in the
    // data but stay off the chart.
    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Siti Aminah",
        bool ihcMember = false,
        string? ihcRole = null,
        string designation = "Halal Executive",
        string? mobile = null)
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@verify.my",
            MobileNumber = mobile,
            TitleId = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr"),
            DepartmentId = await AddPeopleValueAsync(db, companyId, "Department", "Production"),
            DesignationId = await AddPeopleValueAsync(db, companyId, "Designation", designation),
            IsIhcMember = ihcMember,
            IhcRoleId = ihcRole is null
                ? null
                : await AddPeopleValueAsync(
                    db, companyId, "Internal Halal Committee Role", ihcRole)
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A minutes meeting inserted directly (a read test should not have to go through the
    // save path).
    public static async Task<Guid> SeedMeetingAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string title = "IHC Monthly Meeting",
        DateTime? date = null,
        TimeOnly? start = null,
        TimeOnly? end = null,
        string location = "HQ Meeting Room")
    {
        var meeting = new MinutesMeetingEntity
        {
            CompanyId = companyId,
            Title = title,
            MeetingDate = date ?? new DateTime(2026, 4, 15),
            StartTime = start ?? new TimeOnly(9, 0),
            EndTime = end ?? new TimeOnly(11, 0),
            Location = location
        };
        db.MinutesMeetings.Add(meeting);
        await db.SaveChangesAsync();
        return meeting.Id;
    }

    public static CreateMinutesMeetingCommand ValidCreateCommand(
        string title = "IHC Monthly Meeting",
        DateTime? date = null,
        TimeOnly? start = null,
        TimeOnly? end = null,
        string location = "HQ Meeting Room") =>
        new(
            title,
            date ?? new DateTime(2026, 4, 15),
            start ?? new TimeOnly(9, 0),
            end ?? new TimeOnly(11, 0),
            location);

    public static UpdateMinutesMeetingCommand ValidUpdateCommand(
        Guid meetingId,
        string title = "IHC Monthly Meeting",
        DateTime? date = null,
        TimeOnly? start = null,
        TimeOnly? end = null,
        string location = "HQ Meeting Room") =>
        new(
            meetingId,
            title,
            date ?? new DateTime(2026, 4, 15),
            start ?? new TimeOnly(9, 0),
            end ?? new TimeOnly(11, 0),
            location);

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
