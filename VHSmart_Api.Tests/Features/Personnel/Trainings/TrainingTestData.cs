using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

// Fixtures for Personnel > Internal Training: companies, staff, the TRAINING / Module Type
// catalogue rows and a training builder (create/update share the same five fields). Company
// ids are chosen by the test, because every scenario here is about which company the id
// points at.
internal static class TrainingTestData
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

    // A staff row with valid PEOPLE FKs; optionally flagged as an IHC member with a role so
    // the attendance table's "IHC Member" / "Role in IHC" columns have something to show.
    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Siti Aminah",
        bool ihcMember = false,
        string designation = "Halal Executive")
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@verify.my",
            TitleId = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr"),
            DepartmentId = await AddPeopleValueAsync(db, companyId, "Department", "Production"),
            DesignationId = await AddPeopleValueAsync(db, companyId, "Designation", designation),
            IsIhcMember = ihcMember,
            IhcRoleId = ihcMember
                ? await AddPeopleValueAsync(db, companyId, "Internal Halal Committee Role", "Member")
                : null
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // General Data TRAINING / Module Type (spec 7.5 - the module dropdown source).
    public static async Task<Guid> SeedModuleTypeAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Presentation")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.TRAINING,
            Category = "Module Type",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A training with its attendee rows inserted directly (a read test should not have to go
    // through the save path).
    public static async Task<Guid> SeedTrainingAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Halal Awareness 101",
        DateTime? date = null,
        TrainingType trainingType = TrainingType.AllStaff,
        IReadOnlyList<Guid>? attendees = null)
    {
        var training = new TrainingEntity
        {
            CompanyId = companyId,
            TrainingType = trainingType,
            Name = name,
            TrainingDate = date ?? new DateTime(2026, 3, 15)
        };
        db.Trainings.Add(training);
        foreach (var staffId in attendees ?? [])
        {
            db.TrainingAttendees.Add(new TrainingAttendeeEntity
            {
                CompanyId = companyId,
                TrainingId = training.Id,
                StaffId = staffId
            });
        }

        await db.SaveChangesAsync();
        return training.Id;
    }

    public static UpdateTrainingCommand ValidUpdateCommand(
        Guid trainingId,
        TrainingType? trainingType = TrainingType.AllStaff,
        string name = "Halal Awareness 101",
        DateTime? date = null,
        IReadOnlyList<Guid>? attendees = null) =>
        new(
            trainingId,
            trainingType,
            name,
            date ?? new DateTime(2026, 3, 15),
            attendees ?? []);

    public static CreateTrainingCommand ValidCreateCommand(
        TrainingType? trainingType = TrainingType.AllStaff,
        string name = "Halal Awareness 101",
        DateTime? date = null,
        IReadOnlyList<Guid>? attendees = null) =>
        new(
            trainingType,
            name,
            date ?? new DateTime(2026, 3, 15),
            attendees ?? []);

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
