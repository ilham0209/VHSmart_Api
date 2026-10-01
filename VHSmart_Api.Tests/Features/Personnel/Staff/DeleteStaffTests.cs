using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class DeleteStaffTests
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

    private static async Task<StaffEntity> SeedStaffAsync(
        TestableVHSmartDbContext db, Guid companyId)
    {
        var title = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PEOPLE,
            Category = "Title of Honour",
            Name = "Mr"
        };
        var department = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PEOPLE,
            Category = "Department",
            Name = "Production"
        };
        var designation = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PEOPLE,
            Category = "Designation",
            Name = "Halal Executive"
        };
        db.GeneralData.AddRange(title, department, designation);
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Email = "staff@verify.my",
            TitleId = title.Id,
            Name = "Siti Aminah",
            DepartmentId = department.Id,
            DesignationId = designation.Id
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await SeedStaffAsync(db, CompanyA);

        await new DeleteStaffHandler(db, user)
            .Handle(new DeleteStaffCommand(row.Id), CancellationToken.None);

        var stored = await db.Staffs.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Equal(
            0,
            (await new GetAllStaffHandler(db, user)
                .Handle(new GetAllStaffQuery(), CancellationToken.None)).TotalRecords);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteStaffHandler(db, user)
                .Handle(new DeleteStaffCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFoundAndKeepsTheRow()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreign = await SeedStaffAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteStaffHandler(db, userA)
                .Handle(new DeleteStaffCommand(foreign.Id), CancellationToken.None));

        Assert.False((await db.Staffs.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }
}
