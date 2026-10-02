using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

public class GetProfileStaffOptionsTests
{
    [Fact]
    public async Task Handle_ReturnsTargetCompanyStaffWithDesignation()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        await ProfileTestData.SeedStaffAsync(db, user.CompanyId, "Aiman Rahman");
        await ProfileTestData.SeedStaffAsync(db, user.CompanyId, "Nurul Islam", "Manager");

        var options = await new GetProfileStaffOptionsHandler(db, user)
            .Handle(new GetProfileStaffOptionsQuery(user.CompanyId), CancellationToken.None);

        Assert.Equal(2, options.Count);
        Assert.Equal(
            new[] { "Aiman Rahman", "Nurul Islam" },
            options.Select(option => option.Name));
        Assert.Equal("Halal Executive", options[0].Designation);
        Assert.Equal("Manager", options[1].Designation);
        Assert.False(string.IsNullOrWhiteSpace(options[0].Email));
    }

    [Fact]
    public async Task Handle_LeavesOutOtherCompaniesStaff()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var ownCompany = await ProfileTestData.SeedCompanyAsync(db, "Acme Foods");
        var foreignCompany = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");
        await ProfileTestData.SeedStaffAsync(db, ownCompany, "Local Person");
        await ProfileTestData.SeedStaffAsync(db, foreignCompany, "Foreign Person");

        var options = await new GetProfileStaffOptionsHandler(db, user)
            .Handle(new GetProfileStaffOptionsQuery(ownCompany), CancellationToken.None);

        var option = Assert.Single(options);
        Assert.Equal("Local Person", option.Name);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProfileStaffOptionsHandler(db, user)
                .Handle(new GetProfileStaffOptionsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesOptions_AsCompanyUser_ThrowsNotFound()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Own Foods", user.CompanyId);
        var foreignId = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");
        await ProfileTestData.SeedStaffAsync(db, foreignId, "Foreign Person");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProfileStaffOptionsHandler(db, user)
                .Handle(new GetProfileStaffOptionsQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesOptions_AsPlatformAdmin_ReturnsThatStaff()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var foreignId = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");
        await ProfileTestData.SeedStaffAsync(db, foreignId, "Foreign Person");

        var options = await new GetProfileStaffOptionsHandler(db, user)
            .Handle(new GetProfileStaffOptionsQuery(foreignId), CancellationToken.None);

        var option = Assert.Single(options);
        Assert.Equal("Foreign Person", option.Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedStaff_IsNotOffered()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var staffId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId, "Gone Person");
        var staff = await db.Staffs.SingleAsync(row => row.Id == staffId);
        db.Staffs.Remove(staff);
        await db.SaveChangesAsync();

        var options = await new GetProfileStaffOptionsHandler(db, user)
            .Handle(new GetProfileStaffOptionsQuery(user.CompanyId), CancellationToken.None);

        Assert.Empty(options);
    }
}
