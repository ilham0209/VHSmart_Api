using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class GetPremiseByIdTests
{
    [Fact]
    public async Task Handle_ExistingPremise_ReturnsFullDetailWithChildren()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", user.CompanyId);
        var managerId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Ahmad Razali");
        var contactId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Nur Aisyah");
        var prayerRoomId = await PremiseTestData.SeedPrayerRoomAsync(db, user.CompanyId);
        var premiseId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId,
            name: "Seri Rasa Factory",
            storeCode: "SC-01",
            areaManagerStaffId: managerId,
            prayerRoomAvailabilityId: prayerRoomId);

        db.PremiseContacts.Add(new PremiseContactEntity
        {
            CompanyId = user.CompanyId,
            PremiseId = premiseId,
            StaffId = contactId
        });
        db.PremiseHostels.Add(new PremiseHostelEntity
        {
            CompanyId = user.CompanyId,
            PremiseId = premiseId,
            HostelName = "Hostel A",
            Address = "Jalan Penginapan 1",
            ContactPerson = "Penjaga Hostel",
            PhoneNo = "0198765432"
        });
        await db.SaveChangesAsync();

        var response = await new GetPremiseByIdHandler(db, user)
            .Handle(new GetPremiseByIdQuery(premiseId), CancellationToken.None);

        Assert.Equal("Verify Halal Sdn Bhd", response.Company);
        Assert.Equal("Seri Rasa Factory", response.Name);
        Assert.Equal("SC-01", response.StoreCode);
        Assert.Equal("Ahmad Razali", response.AreaManager);
        Assert.Equal(managerId, response.AreaManagerStaffId);
        Assert.Equal("Available", response.PrayerRoomAvailability);
        var contact = Assert.Single(response.Contacts);
        Assert.Equal("Nur Aisyah", contact.Name);
        Assert.Equal("Halal Executive", contact.Designation);
        var hostel = Assert.Single(response.Hostels);
        Assert.Equal("Hostel A", hostel.HostelName);
        Assert.Equal("Jalan Penginapan 1", hostel.Address);
    }

    [Fact]
    public async Task Handle_PremiseManagerFromStaff_ResolvesTheStaffName()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var managerId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Pengurus Utama");
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        premise.PremiseManagerStaffId = managerId;
        await db.SaveChangesAsync();

        var response = await new GetPremiseByIdHandler(db, user)
            .Handle(new GetPremiseByIdQuery(premiseId), CancellationToken.None);

        Assert.Equal("Pengurus Utama", response.PremiseManager);
        Assert.Equal(managerId, response.PremiseManagerStaffId);
        Assert.Null(response.PremiseManagerName);
    }

    [Fact]
    public async Task Handle_PremiseManagerTypedName_FallsBackToTheTypedName()
    {
        // Spec 7.7 "Select Premise Manager from All Staff Information?" = No: the name is
        // typed into the field and travels as PremiseManagerName.
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        premise.PremiseManagerName = "Typed Manager";
        await db.SaveChangesAsync();

        var response = await new GetPremiseByIdHandler(db, user)
            .Handle(new GetPremiseByIdQuery(premiseId), CancellationToken.None);

        Assert.Equal("Typed Manager", response.PremiseManager);
        Assert.Null(response.PremiseManagerStaffId);
        Assert.Equal("Typed Manager", response.PremiseManagerName);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var foreignId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, "Foreign premise");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseByIdHandler(db, user)
                .Handle(new GetPremiseByIdQuery(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseByIdHandler(db, user)
                .Handle(new GetPremiseByIdQuery(foreignId), CancellationToken.None));
    }
}
