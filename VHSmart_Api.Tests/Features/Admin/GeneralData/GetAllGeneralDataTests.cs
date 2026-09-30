using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class GetAllGeneralDataTests
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

    private static GeneralDataEntity NewRow(
        Guid companyId,
        GeneralDataGroup group,
        string category,
        string name,
        string? description = null) =>
        new()
        {
            CompanyId = companyId,
            Group = group,
            Category = category,
            Name = name,
            Description = description
        };

    [Fact]
    public async Task Handle_GroupAndCategoryFilters_ReturnOnlyMatchingRows()
    {
        var db = await CreateDbAsync(UserA());
        db.GeneralData.AddRange(
            NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Amanah"),
            NewRow(CompanyA, GeneralDataGroup.COMPANY, "Ownership Type", "Private Limited"),
            NewRow(CompanyA, GeneralDataGroup.AUDIT, "Audit Type", "Internal"));
        await db.SaveChangesAsync();

        var byGroup = await new GetAllGeneralDataHandler(db)
            .Handle(new GetAllGeneralDataQuery { Group = GeneralDataGroup.AUDIT }, CancellationToken.None);
        var byGroupAndCategory = await new GetAllGeneralDataHandler(db)
            .Handle(
                new GetAllGeneralDataQuery
                {
                    Group = GeneralDataGroup.COMPANY,
                    Category = "Brand"
                },
                CancellationToken.None);
        var unfiltered = await new GetAllGeneralDataHandler(db)
            .Handle(new GetAllGeneralDataQuery(), CancellationToken.None);

        Assert.Equal("Internal", Assert.Single(byGroup.Data).Name);
        Assert.Equal("Amanah", Assert.Single(byGroupAndCategory.Data).Name);
        Assert.Equal(3, unfiltered.TotalRecords);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        db.GeneralData.AddRange(
            NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Company A brand"),
            NewRow(CompanyB, GeneralDataGroup.COMPANY, "Brand", "Company B brand"));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllGeneralDataHandler(db)
            .Handle(new GetAllGeneralDataQuery(), CancellationToken.None);

        Assert.Equal(1, asCompanyA.TotalRecords);
        Assert.Equal("Company A brand", Assert.Single(asCompanyA.Data).Name);

        // A second context for company B on the same store sees only its own row.
        var dbB = TestDbFactory.Create(databaseName, UserB());
        var asCompanyB = await new GetAllGeneralDataHandler(dbB)
            .Handle(new GetAllGeneralDataQuery(), CancellationToken.None);

        Assert.Equal(1, asCompanyB.TotalRecords);
        Assert.Equal("Company B brand", Assert.Single(asCompanyB.Data).Name);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsNameOrDescription()
    {
        var db = await CreateDbAsync(UserA());
        db.GeneralData.AddRange(
            NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Sahih Mart"),
            NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Other", "halal supply chain"),
            NewRow(CompanyA, GeneralDataGroup.PEOPLE, "Designation", "Auditor"));
        await db.SaveChangesAsync();

        var byName = await new GetAllGeneralDataHandler(db)
            .Handle(
                new GetAllGeneralDataQuery { Request = new DataGridRequest { SearchTerm = "Sahih" } },
                CancellationToken.None);
        var byDescription = await new GetAllGeneralDataHandler(db)
            .Handle(
                new GetAllGeneralDataQuery { Request = new DataGridRequest { SearchTerm = "supply" } },
                CancellationToken.None);

        Assert.Equal("Sahih Mart", Assert.Single(byName.Data).Name);
        Assert.Equal("Other", Assert.Single(byDescription.Data).Name);
    }

    [Fact]
    public async Task Handle_SortBySysDateModified_NewestFirst()
    {
        var db = await CreateDbAsync(UserA());
        var older = NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Older brand");
        older.SysDateModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Newer brand");
        newer.SysDateModified = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        db.GeneralData.AddRange(older, newer);
        await db.SaveChangesAsync();

        var result = await new GetAllGeneralDataHandler(db)
            .Handle(
                new GetAllGeneralDataQuery
                {
                    Request = new DataGridRequest
                    {
                        SortBy = "SysDateModified",
                        SortDescending = true
                    }
                },
                CancellationToken.None);

        Assert.Equal(["Newer brand", "Older brand"], result.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        var row = NewRow(CompanyA, GeneralDataGroup.COMPANY, "Brand", "Doomed brand");
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        db.GeneralData.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllGeneralDataHandler(db)
            .Handle(new GetAllGeneralDataQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }
}
