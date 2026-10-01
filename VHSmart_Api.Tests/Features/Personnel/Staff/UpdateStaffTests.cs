using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class UpdateStaffTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Guid> AddPeopleValueAsync(
        TestableVHSmartDbContext db, Guid companyId, string category, string name)
    {
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

    private static async Task<StaffEntity> SeedStaffAsync(
        TestableVHSmartDbContext db, Guid companyId, string email = "staff@verify.my")
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Email = email,
            TitleId = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr"),
            Name = "Siti Aminah",
            DepartmentId = await AddPeopleValueAsync(db, companyId, "Department", "Production"),
            DesignationId = await AddPeopleValueAsync(db, companyId, "Designation", "Halal Executive")
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static async Task<UpdateStaffCommand> CommandAsync(
        TestableVHSmartDbContext db, StaffEntity staff) =>
        new(
            staff.Id,
            staff.Email,
            staff.TitleId,
            "Siti Aminah Binti Omar",
            "NRIC",
            "900101011234",
            "EMP-01",
            "Female",
            "Islam",
            staff.DepartmentId,
            staff.DesignationId,
            "0312345678",
            "0123456789",
            true,
            new DateTime(2027, 1, 1),
            false,
            null);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesFieldsAndKeepsCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var command = await CommandAsync(db, staff);

        var response = await new UpdateStaffHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Siti Aminah Binti Omar", response.Name);

        var stored = await db.Staffs.AsNoTracking().SingleAsync();
        Assert.Equal("Siti Aminah Binti Omar", stored.Name);
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.True(stored.HasTyphoidInjection);
        Assert.Equal(new DateTime(2027, 1, 1), stored.TyphoidExpiryDate);
    }

    [Fact]
    public async Task Handle_DuplicateEmailOfAnotherStaff_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var first = await SeedStaffAsync(db, CompanyA, email: "first@verify.my");
        var second = await SeedStaffAsync(db, CompanyA, email: "second@verify.my");
        var command = await CommandAsync(db, second) with { Email = first.Email };

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateStaffHandler(db, user).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnchangedOwnEmail_DoesNotConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var command = await CommandAsync(db, staff);

        var response = await new UpdateStaffHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal(staff.Id, response.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var command = await CommandAsync(db, staff) with { Id = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateStaffHandler(db, user).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFoundAndKeepsTheRow()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreign = await SeedStaffAsync(db, CompanyB, email: "foreign@verify.my");
        var command = await CommandAsync(db, foreign);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateStaffHandler(db, userA).Handle(command, CancellationToken.None));

        var stored = await db.Staffs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Siti Aminah", stored.Name);
        Assert.False(stored.IsDeleted);
    }
}
