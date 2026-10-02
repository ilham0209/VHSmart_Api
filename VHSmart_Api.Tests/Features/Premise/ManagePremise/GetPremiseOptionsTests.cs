using VHSmart_Api.Features.Premise.ManagePremise;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class GetPremiseOptionsTests
{
    [Fact]
    public async Task Handle_ReturnsFourPremiseTypesInTheSpecOrder()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);

        var response = await new GetPremiseOptionsHandler(db, user)
            .Handle(new GetPremiseOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "Factory", "CentralKitchen", "ColdRoomAndWarehouse", "RestaurantsAndCafe"
            },
            response.PremiseTypes);
    }

    [Fact]
    public async Task Handle_BrandsAndPrayerRooms_AreOwnCompanyOnly()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedBrandAsync(db, user.CompanyId, "Seri Rasa");
        await PremiseTestData.SeedBrandAsync(db, otherCompany.CompanyId, "Foreign Brand");
        await PremiseTestData.SeedPrayerRoomAsync(db, user.CompanyId, "Available");
        await PremiseTestData.SeedPrayerRoomAsync(db, otherCompany.CompanyId, "Foreign Prayer");

        var response = await new GetPremiseOptionsHandler(db, user)
            .Handle(new GetPremiseOptionsQuery(), CancellationToken.None);

        var brand = Assert.Single(response.Brands);
        Assert.Equal("Seri Rasa", brand.Name);
        var prayerRoom = Assert.Single(response.PrayerRoomAvailabilities);
        Assert.Equal("Available", prayerRoom.Name);
    }

    [Fact]
    public async Task Handle_StaffOptions_CarryContactDetails()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Nur Aisyah");
        await PremiseTestData.SeedStaffAsync(db, otherCompany.CompanyId, "Foreign Staff");

        var response = await new GetPremiseOptionsHandler(db, user)
            .Handle(new GetPremiseOptionsQuery(), CancellationToken.None);

        var staff = Assert.Single(response.Staff);
        Assert.Equal("Nur Aisyah", staff.Name);
        Assert.Equal("Halal Executive", staff.Designation);
        Assert.False(string.IsNullOrWhiteSpace(staff.Email));
        Assert.Equal("0123456789", staff.MobileNumber);
    }

    [Fact]
    public async Task Handle_PremiseTags_AreOwnCompanyOnly()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedPremiseTagAsync(db, user.CompanyId, "Complete Documentation");
        await PremiseTestData.SeedPremiseTagAsync(db, otherCompany.CompanyId, "Foreign tag");

        var response = await new GetPremiseOptionsHandler(db, user)
            .Handle(new GetPremiseOptionsQuery(), CancellationToken.None);

        var tag = Assert.Single(response.PremiseTags);
        Assert.Equal("Complete Documentation", tag.Name);
    }
}
