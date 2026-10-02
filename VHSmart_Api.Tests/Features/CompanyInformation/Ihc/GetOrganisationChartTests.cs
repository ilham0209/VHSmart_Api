using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Ihc;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class GetOrganisationChartTests
{
    [Fact]
    public async Task Handle_OrdersByRoleThenName_AndShowsTheTableColumns()
    {
        var companyId = Guid.NewGuid();
        var user = MinutesMeetingTestData.CompanyUser(companyId);
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        await MinutesMeetingTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", companyId);
        // Alphabetical role order happens to be the spec's top-down chart order (7.6).
        await MinutesMeetingTestData.SeedStaffAsync(
            db, companyId, "Slaughterman Ali", ihcMember: true, ihcRole: "Slaughterman",
            designation: "Slaughterman", mobile: "011-2222333");
        await MinutesMeetingTestData.SeedStaffAsync(
            db, companyId, "Executive Fatimah", ihcMember: true, ihcRole: "Halal Executive",
            mobile: "012-3456789");
        await MinutesMeetingTestData.SeedStaffAsync(
            db, companyId, "Chairman Ahmad", ihcMember: true, ihcRole: "Halal Chairman");

        var rows = await new GetOrganisationChartHandler(db, user)
            .Handle(new GetOrganisationChartQuery(), CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.Equal(
            new[] { "Halal Chairman", "Halal Executive", "Slaughterman" },
            rows.Select(row => row.Role));
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(row => row.No));

        var executive = rows[1];
        Assert.Equal("Executive Fatimah", executive.Name);
        Assert.Equal("Halal Executive", executive.Role);
        Assert.Equal("Production", executive.Department);
        Assert.False(string.IsNullOrWhiteSpace(executive.Email));
        Assert.Equal("012-3456789", executive.MobileNo);
        Assert.Equal("Verify Halal Sdn Bhd", executive.Company);
    }

    [Fact]
    public async Task Handle_ExcludesMembersWithoutRoleNonMembersAndOtherCompanies()
    {
        var companyId = Guid.NewGuid();
        var user = MinutesMeetingTestData.CompanyUser(companyId);
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        // Flagged as an IHC member but without a role - the spec chart needs both halves.
        await MinutesMeetingTestData.SeedStaffAsync(
            db, companyId, "Roleless Member", ihcMember: true, ihcRole: null);
        await MinutesMeetingTestData.SeedStaffAsync(db, companyId, "Plain Staff");
        var foreignCompany = await MinutesMeetingTestData.SeedCompanyAsync(db, "Foreign company");
        await MinutesMeetingTestData.SeedStaffAsync(
            db, foreignCompany, "Foreign Chairman",
            ihcMember: true, ihcRole: "Halal Chairman");
        var deletedId = await MinutesMeetingTestData.SeedStaffAsync(
            db, companyId, "Gone Chairman", ihcMember: true, ihcRole: "Halal Chairman");
        var deleted = await db.Staffs.SingleAsync(row => row.Id == deletedId);
        db.Staffs.Remove(deleted);
        await db.SaveChangesAsync();

        var rows = await new GetOrganisationChartHandler(db, user)
            .Handle(new GetOrganisationChartQuery(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_CompanyCellShowsTheCallersCompany()
    {
        var companyId = Guid.NewGuid();
        var user = MinutesMeetingTestData.CompanyUser(companyId);
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        await MinutesMeetingTestData.SeedCompanyAsync(db, "My Company Sdn Bhd", companyId);
        await MinutesMeetingTestData.SeedStaffAsync(
            db, companyId, "Chairman Ahmad", ihcMember: true, ihcRole: "Halal Chairman");

        var rows = await new GetOrganisationChartHandler(db, user)
            .Handle(new GetOrganisationChartQuery(), CancellationToken.None);

        Assert.Equal("My Company Sdn Bhd", Assert.Single(rows).Company);
    }
}
