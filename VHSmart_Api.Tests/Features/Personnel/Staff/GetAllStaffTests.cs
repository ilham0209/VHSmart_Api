using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class GetAllStaffTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

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

    private static async Task<StaffEntity> NewRowAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name,
        string email)
    {
        var designation = await AddPeopleValueAsync(db, companyId, "Designation", name);
        var title = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr");
        var department = await AddPeopleValueAsync(db, companyId, "Department", "Production");
        return new StaffEntity
        {
            CompanyId = companyId,
            Email = email,
            TitleId = title,
            Name = name,
            DepartmentId = department,
            DesignationId = designation
        };
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsNameOrEmail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        db.Staffs.AddRange(
            await NewRowAsync(db, CompanyA, "Siti Aminah", "siti@verify.my"),
            await NewRowAsync(db, CompanyA, "Abu Bakar", "abu@verify.my"));
        await db.SaveChangesAsync();

        var byName = await QueryAsync(db, user, "Siti");
        var byEmail = await QueryAsync(db, user, "abu@");

        Assert.Equal("Siti Aminah", Assert.Single(byName.Data).Name);
        Assert.Equal("Abu Bakar", Assert.Single(byEmail.Data).Name);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var userB = UserB();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        db.Staffs.AddRange(
            await NewRowAsync(db, CompanyA, "Company A staff", "a@verify.my"),
            await NewRowAsync(db, CompanyB, "Company B staff", "b@verify.my"));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllStaffHandler(db, userA)
            .Handle(new GetAllStaffQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllStaffHandler(
                TestDbFactory.Create(databaseName, userB), userB)
            .Handle(new GetAllStaffQuery(), CancellationToken.None);

        Assert.Equal("Company A staff", Assert.Single(asCompanyA.Data).Name);
        Assert.Equal("Company B staff", Assert.Single(asCompanyB.Data).Name);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsNameAscendingAndNumberedFromOne_WithDesignationAndCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        db.Staffs.AddRange(
            await NewRowAsync(db, CompanyA, "Zulkifli Omar", "zul@verify.my"),
            await NewRowAsync(db, CompanyA, "Aisyah Rahman", "aisyah@verify.my"));
        await db.SaveChangesAsync();
        db.Companies.Add(new CompanyEntity
        {
            Id = CompanyA,
            Name = "Verify Halal Sdn Bhd",
            BusinessRegistrationNo = "202600000001",
            Address1 = "1 Jalan Verify",
            Address2 = string.Empty,
            PostCode = "50000",
            District = "Kuala Lumpur",
            State = "Kuala Lumpur",
            Telephone = "0312345678",
            Email = "hq@verify.my"
        });
        await db.SaveChangesAsync();

        var result = await new GetAllStaffHandler(db, user)
            .Handle(new GetAllStaffQuery(), CancellationToken.None);

        Assert.Equal(["Aisyah Rahman", "Zulkifli Omar"], result.Data.Select(row => row.Name).ToArray());
        // "No." is the position in the whole list, starting at 1 (spec 7.4 first column).
        Assert.Equal([1, 2], result.Data.Select(row => row.No).ToArray());
        var rows = result.Data.ToArray();
        Assert.Equal("Verify Halal Sdn Bhd", rows[0].Company);
        Assert.Equal("Aisyah Rahman", rows[0].Designation);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await NewRowAsync(db, CompanyA, "Doomed staff", "doomed@verify.my");
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        db.Staffs.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllStaffHandler(db, user)
            .Handle(new GetAllStaffQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    private static Task<DataGridResponse<GetAllStaffResponse>> QueryAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        string searchTerm) =>
        new GetAllStaffHandler(db, user).Handle(
            new GetAllStaffQuery { Request = new DataGridRequest { SearchTerm = searchTerm } },
            CancellationToken.None);
}
