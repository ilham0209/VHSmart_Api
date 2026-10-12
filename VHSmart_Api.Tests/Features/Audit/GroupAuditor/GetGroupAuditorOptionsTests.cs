using VHSmart_Api.Features.Audit.GroupAuditor;
using static VHSmart_Api.Tests.Features.Audit.GroupAuditor.GroupAuditorTestData;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class GetGroupAuditorOptionsTests
{
    private static GetGroupAuditorOptionsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ReturnsOwnChecklistsWithCategoryAndName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, CompanyA, "Internal Supplier");
        await SeedChecklistAsync(db, CompanyA, category, "Checklist Alpha");
        await SeedChecklistAsync(db, CompanyA, category, "Checklist Beta");

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        Assert.Equal(2, response.Templates.Count);
        Assert.All(response.Templates, row => Assert.Equal("Internal Supplier", row.Category));
        Assert.Equal(["Checklist Alpha", "Checklist Beta"],
            response.Templates.Select(row => row.Name).ToList());
    }

    [Fact]
    public async Task Handle_Templates_SortByCategoryThenName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var zuluCategory = await SeedChecklistCategoryAsync(db, CompanyA, "Zulu Category");
        var alphaCategory = await SeedChecklistCategoryAsync(db, CompanyA, "Alpha Category");
        await SeedChecklistAsync(db, CompanyA, zuluCategory, "Checklist A");
        await SeedChecklistAsync(db, CompanyA, alphaCategory, "Checklist Z");

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        Assert.Equal(["Checklist Z", "Checklist A"],
            response.Templates.Select(row => row.Name).ToList());
    }

    [Fact]
    public async Task Handle_Templates_ExcludeForeignRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedChecklistAsync(db, CompanyA, name: "Own Checklist");
        await SeedChecklistAsync(db, CompanyB, name: "Foreign Checklist");

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        Assert.Equal("Own Checklist", Assert.Single(response.Templates).Name);
    }

    [Fact]
    public async Task Handle_Auditors_OnlyOwnCompanyAuditorRoleUsers()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedAuditorAsync(db, CompanyA, name: "AUDITOR ONE");
        await SeedNonAuditorAsync(db, CompanyA);
        await SeedAuditorAsync(db, CompanyB, name: "FOREIGN AUDITOR");

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        Assert.Equal("AUDITOR ONE", Assert.Single(response.Auditors).Name);
    }

    [Fact]
    public async Task Handle_Auditors_ReturnContactNoAndEmail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedAuditorAsync(db, CompanyA, name: "AUDITOR ONE", contactNo: "012-999888");

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        var row = Assert.Single(response.Auditors);
        Assert.Equal("012-999888", row.ContactNo);
        Assert.False(string.IsNullOrWhiteSpace(row.Email));
    }

    [Fact]
    public async Task Handle_Auditors_InactiveUsersAreStillListed()
    {
        // D-10 defines an auditor by role only - it states no IsActive condition (flagged).
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedAuditorAsync(db, CompanyA, name: "DEACTIVATED", isActive: false);

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        Assert.Equal("DEACTIVATED", Assert.Single(response.Auditors).Name);
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyLists()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorOptionsQuery(), CancellationToken.None);

        Assert.Empty(response.Templates);
        Assert.Empty(response.Auditors);
    }
}
