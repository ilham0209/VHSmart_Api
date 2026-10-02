using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class GetAllPremisesTests
{
    [Fact]
    public async Task Handle_DefaultSort_IsStoreNameAscendingWithJoins()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", user.CompanyId);
        var managerId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Ahmad Razali");
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Zeta Depot",
            storeCode: "SC-90", areaManagerStaffId: managerId, address3: "Lot 3");
        await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, name: "Alpha Kitchen");

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Alpha Kitchen", "Zeta Depot" }, rows.Select(row => row.StoreName));
        // Address composes line 1 + line 2 + optional line 3, trimmed (spec 7.7 list column).
        Assert.Equal("1 Jalan Verify Taman Industri", rows[0].Address);
        Assert.Equal("1 Jalan Verify Taman Industri Lot 3", rows[1].Address);
        Assert.Equal("Ahmad Razali", rows[1].AreaManager);
        Assert.Equal("SC-90", rows[1].StoreCode);
        Assert.Equal("Shah Alam", rows[0].City);
        Assert.Equal("Selangor", rows[0].State);
        Assert.Equal(
            await db.Countries
                .Where(c => c.IsoCode == "MYS")
                .Select(c => c.Name)
                .SingleAsync(),
            rows[0].Country);
        Assert.Equal(PremiseType.Factory, rows[0].PremiseType);
    }

    [Fact]
    public async Task Handle_PremiseTypeFilter_ReturnsOnlyMatchingRows()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Cold Store", premiseType: PremiseType.ColdRoomAndWarehouse);
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Rosa Cafe", premiseType: PremiseType.RestaurantsAndCafe);

        var all = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);
        var onlyCafe = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery { PremiseType = PremiseType.RestaurantsAndCafe },
                CancellationToken.None);

        Assert.Equal(2, all.TotalRecords);
        Assert.Equal("Rosa Cafe", Assert.Single(onlyCafe.Data).StoreName);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsStoreCodeNameCityOrEmail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Main Factory", storeCode: "SC-01");
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Branch Kitchen", city: "Penang");

        var byStoreCode = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery
            {
                Request = new DataGridRequest { SearchTerm = "SC-01" }
            }, CancellationToken.None);
        var byCity = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery
            {
                Request = new DataGridRequest { SearchTerm = "penang" }
            }, CancellationToken.None);

        Assert.Equal("Main Factory", Assert.Single(byStoreCode.Data).StoreName);
        Assert.Equal("Branch Kitchen", Assert.Single(byCity.Data).StoreName);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = PremiseTestData.CompanyUser();
        var userB = PremiseTestData.CompanyUser();
        var databaseName = TestDbFactory.NewDatabaseName();
        var dbA = await PremiseTestData.CreateDbAsync(userA, databaseName);
        await PremiseTestData.SeedPremiseAsync(dbA, userA.CompanyId, name: "Company A premise");
        await PremiseTestData.SeedPremiseAsync(dbA, userB.CompanyId, name: "Company B premise");

        var asCompanyA = await new GetAllPremisesHandler(dbA, userA)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllPremisesHandler(
                await PremiseTestData.CreateDbAsync(userB, databaseName), userB)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        Assert.Equal("Company A premise", Assert.Single(asCompanyA.Data).StoreName);
        Assert.Equal("Company B premise", Assert.Single(asCompanyB.Data).StoreName);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, "Doomed premise");
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        db.Premises.Remove(premise);
        await db.SaveChangesAsync();

        var grid = await new GetAllPremisesHandler(db, user)
            .Handle(new GetAllPremisesQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
    }
}
