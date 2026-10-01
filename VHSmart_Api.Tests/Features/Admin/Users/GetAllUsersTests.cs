using VHSmart_Api.Features.Admin.Users;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class GetAllUsersTests
{
    [Fact]
    public async Task Handle_PlatformAdmin_SeesUsersOfAllCompaniesWithColumns()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var userA = await UsersTestData.AddUserAsync(db, "a-user@example.com", companyA);
        var userB = await UsersTestData.AddUserAsync(db, "b-user@example.com", companyB);
        userA.IsActive = false;
        await db.SaveChangesAsync();

        var grid = await new GetAllUsersHandler(db, caller)
            .Handle(new GetAllUsersQuery(), default);

        Assert.Equal(2, grid.TotalRecords);

        var rowA = Assert.Single(grid.Data, row => row.Id == userA.Id);
        Assert.Equal("TEST USER", rowA.Name);
        Assert.Equal("a-user@example.com", rowA.Email);
        Assert.Equal("VH Smart Admin", rowA.Role);
        Assert.Equal("ALPHA SDN BHD", Assert.Single(rowA.Companies));
        Assert.Equal("Inactive", rowA.Status);
        Assert.Equal(userA.SysDateCreated, rowA.CreatedDate);

        var rowB = Assert.Single(grid.Data, row => row.Id == userB.Id);
        Assert.Equal("Active", rowB.Status);
        Assert.Equal("BETA SDN BHD", Assert.Single(rowB.Companies));
    }

    [Fact]
    public async Task Handle_CompanyAdmin_SeesOnlyUsersOfActiveCompany()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var seedCaller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, seedCaller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var userA = await UsersTestData.AddUserAsync(db, "a-user@example.com", companyA);
        _ = await UsersTestData.AddUserAsync(db, "b-user@example.com", companyB);

        var companyAdmin = UsersTestData.CompanyAdmin(companyA);
        var grid = await new GetAllUsersHandler(db, companyAdmin)
            .Handle(new GetAllUsersQuery(), default);

        var single = Assert.Single(grid.Data);
        Assert.Equal(userA.Id, single.Id);
    }

    [Fact]
    public async Task Handle_UserLinkedToSeveralCompanies_ShowsAllNamesSorted()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ZETA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "multi@example.com", companyA, companyB);

        var grid = await new GetAllUsersHandler(db, caller)
            .Handle(new GetAllUsersQuery(), default);

        var row = Assert.Single(grid.Data);
        Assert.Equal(user.Id, row.Id);
        Assert.Equal(2, row.Companies.Count);
        Assert.Equal("ALPHA SDN BHD", row.Companies[0]);
        Assert.Equal("ZETA SDN BHD", row.Companies[1]);
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersByNameAndEmail()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        _ = await UsersTestData.AddUserAsync(db, "ahmad@example.com", company);
        _ = await UsersTestData.AddUserAsync(db, "siti@example.com", company);

        var byEmail = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery
            {
                Request = new() { SearchTerm = "SITI" }
            },
            default);
        Assert.Single(byEmail.Data);

        var byName = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery
            {
                Request = new() { SearchTerm = "ahmad" }
            },
            default);
        Assert.Single(byName.Data);

        var noMatch = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery
            {
                Request = new() { SearchTerm = "nobody" }
            },
            default);
        Assert.Empty(noMatch.Data);
        Assert.Equal(0, noMatch.TotalRecords);
    }

    [Fact]
    public async Task Handle_SortByNameDescending_ReturnsOrderedPage()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var first = await UsersTestData.AddUserAsync(db, "aaa@example.com", company);
        first.Name = "AAA USER";
        var second = await UsersTestData.AddUserAsync(db, "zzz@example.com", company);
        second.Name = "ZZZ USER";
        await db.SaveChangesAsync();

        var grid = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery
            {
                Request = new() { SortBy = "Name", SortDescending = true }
            },
            default);

        var names = grid.Data.Select(row => row.Name).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal("ZZZ USER", names[0]);
        Assert.Equal("AAA USER", names[1]);
    }

    [Fact]
    public async Task Handle_Paging_ClampsPageSizeAndSlicesRows()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        for (var index = 0; index < 12; index++)
            await UsersTestData.AddUserAsync(db, $"user{index}@example.com", company);

        var page1 = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery { Request = new() { Page = 1, PageSize = 10 } }, default);
        Assert.Equal(10, page1.Data.Count());
        Assert.Equal(12, page1.TotalRecords);
        Assert.Equal(2, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        var page2 = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery { Request = new() { Page = 2, PageSize = 10 } }, default);
        Assert.Equal(2, page2.Data.Count());
        Assert.True(page2.HasPreviousPage);

        // A hostile page size cannot pull the whole table (shared clamping rules).
        var huge = await new GetAllUsersHandler(db, caller).Handle(
            new GetAllUsersQuery { Request = new() { Page = 1, PageSize = 100000 } }, default);
        Assert.Equal(12, huge.Data.Count());
        Assert.Equal(100, huge.PageSize);
    }

    [Fact]
    public async Task Handle_NoUsers_ReturnsEmptyGrid()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);

        var grid = await new GetAllUsersHandler(db, caller)
            .Handle(new GetAllUsersQuery(), default);

        Assert.Empty(grid.Data);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_SoftDeletedUser_IsHidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "gone@example.com", company);
        user.IsDeleted = true;
        await db.SaveChangesAsync();

        var grid = await new GetAllUsersHandler(db, caller)
            .Handle(new GetAllUsersQuery(), default);

        Assert.Empty(grid.Data);
    }
}
