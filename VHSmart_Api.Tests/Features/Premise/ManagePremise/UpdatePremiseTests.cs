using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class UpdatePremiseTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesFieldsAndReturnsDetail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", user.CompanyId);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        var response = await new UpdatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidUpdateCommand(
                premiseId, countryId,
                premiseType: PremiseType.ColdRoomAndWarehouse,
                name: "Renamed Cold Store",
                storeCode: "SC-77"),
                CancellationToken.None);

        Assert.Equal("Renamed Cold Store", response.Name);
        Assert.Equal(PremiseType.ColdRoomAndWarehouse, response.PremiseType);
        Assert.Equal("SC-77", response.StoreCode);
        Assert.Equal("Verify Halal Sdn Bhd", response.Company);
        var stored = await db.Premises.AsNoTracking().SingleAsync();
        Assert.Equal("Renamed Cold Store", stored.Name);
        Assert.Equal(user.UserId, stored.SysUserModified);
    }

    [Fact]
    public async Task Handle_DuplicateEmailOrStoreCodeOfAnotherPremise_ThrowsConflict()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseA = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Premise A", email: "a@premise.my", storeCode: "SC-A");
        await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, name: "Premise B", email: "b@premise.my", storeCode: "SC-B");

        var emailConflict = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdatePremiseHandler(db, user)
                .Handle(PremiseTestData.ValidUpdateCommand(
                    premiseA, countryId, email: "b@premise.my"),
                    CancellationToken.None));
        var storeCodeConflict = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdatePremiseHandler(db, user)
                .Handle(PremiseTestData.ValidUpdateCommand(
                    premiseA, countryId, storeCode: "SC-B"),
                    CancellationToken.None));

        Assert.Equal("A premise with this e-mail already exists.", emailConflict.Message);
        Assert.Equal("A premise with this store code already exists.", storeCodeConflict.Message);
    }

    [Fact]
    public async Task Handle_KeepingOwnEmailAndStoreCode_IsAllowed()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, email: "own@premise.my", storeCode: "SC-OWN");

        var response = await new UpdatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidUpdateCommand(
                premiseId, countryId, email: "own@premise.my", storeCode: "SC-OWN"),
                CancellationToken.None);

        Assert.Equal("own@premise.my", response.Email);
        Assert.Equal("SC-OWN", response.StoreCode);
    }

    [Fact]
    public async Task Handle_ContactList_ReconcilesRows()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var keepId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Keep Me");
        var dropId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Drop Me");
        var addId = await PremiseTestData.SeedStaffAsync(db, user.CompanyId, "Add Me");
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        db.PremiseContacts.Add(new PremiseContactEntity
        {
            CompanyId = user.CompanyId,
            PremiseId = premiseId,
            StaffId = keepId
        });
        db.PremiseContacts.Add(new PremiseContactEntity
        {
            CompanyId = user.CompanyId,
            PremiseId = premiseId,
            StaffId = dropId
        });
        await db.SaveChangesAsync();

        var response = await new UpdatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidUpdateCommand(
                premiseId, countryId,
                contactStaffIds: [keepId, addId]),
                CancellationToken.None);

        Assert.Equal(2, response.Contacts.Count);
        Assert.Equal(new[] { "Add Me", "Keep Me" },
            response.Contacts.Select(row => row.Name).Order());
        // The dropped row is soft-deleted, not removed: the filtered UQ (PremiseId, StaffId)
        // frees the pair for a later re-pick.
        var dropped = await db.PremiseContacts
            .IgnoreQueryFilters()
            .SingleAsync(row => row.PremiseId == premiseId && row.StaffId == dropId);
        Assert.True(dropped.IsDeleted);
        var kept = await db.PremiseContacts
            .SingleAsync(row => row.PremiseId == premiseId && row.StaffId == keepId);
        Assert.False(kept.IsDeleted);
    }

    [Fact]
    public async Task Handle_HostelList_ReconcilesByInputId()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var keepHostelId = Guid.NewGuid();
        var dropHostelId = Guid.NewGuid();
        db.PremiseHostels.AddRange(
            new PremiseHostelEntity
            {
                Id = keepHostelId,
                CompanyId = user.CompanyId,
                PremiseId = premiseId,
                HostelName = "Hostel A"
            },
            new PremiseHostelEntity
            {
                Id = dropHostelId,
                CompanyId = user.CompanyId,
                PremiseId = premiseId,
                HostelName = "Hostel B"
            });
        await db.SaveChangesAsync();

        var response = await new UpdatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidUpdateCommand(
                premiseId, countryId,
                hostels:
                [
                    new PremiseHostelInput(keepHostelId, "Hostel A Renamed", "Jalan Baru",
                        new DateTime(2027, 1, 31), "Penjaga", "0111111111"),
                    new PremiseHostelInput(null, "Hostel C", null, null, null, null)
                ]),
                CancellationToken.None);

        Assert.Equal(2, response.Hostels.Count);
        var renamed = Assert.Single(response.Hostels, row => row.HostelName == "Hostel A Renamed");
        Assert.Equal(keepHostelId, renamed.Id);
        Assert.Equal(new DateTime(2027, 1, 31), renamed.TenancyExpiryDate);
        Assert.Equal("Hostel C", Assert.Single(response.Hostels, row => row.Id != keepHostelId).HostelName);
        var dropped = await db.PremiseHostels
            .IgnoreQueryFilters()
            .SingleAsync(row => row.Id == dropHostelId);
        Assert.True(dropped.IsDeleted);
    }

    [Fact]
    public async Task Handle_ForeignPremise_ThrowsNotFoundAndChangesNothing()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var foreignId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, name: "Foreign premise");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdatePremiseHandler(db, user)
                .Handle(PremiseTestData.ValidUpdateCommand(
                    foreignId, countryId, name: "Renamed"),
                    CancellationToken.None));
        var stored = await db.Premises
            .IgnoreQueryFilters()
            .SingleAsync(row => row.Id == foreignId);
        Assert.Equal("Foreign premise", stored.Name);
    }

    [Fact]
    public async Task Handle_UnknownHostelId_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdatePremiseHandler(db, user)
                .Handle(PremiseTestData.ValidUpdateCommand(
                    premiseId, countryId,
                    hostels: [new PremiseHostelInput(Guid.NewGuid(), "Ghost Hostel",
                        null, null, null, null)]),
                    CancellationToken.None));
        Assert.Equal(0, await db.PremiseHostels.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_UnknownContactPerson_Fails()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var validator = new UpdatePremiseValidator(db, user);

        var result = await validator.ValidateAsync(
            PremiseTestData.ValidUpdateCommand(
                premiseId, countryId, contactStaffIds: [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Contact person not found.");
    }

    [Fact]
    public async Task Validator_HostelWithoutName_Fails()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var validator = new UpdatePremiseValidator(db, user);

        var result = await validator.ValidateAsync(
            PremiseTestData.ValidUpdateCommand(
                premiseId, countryId,
                hostels: [new PremiseHostelInput(null, " ", null, null, null, null)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Hostel name is required.");
    }
}
