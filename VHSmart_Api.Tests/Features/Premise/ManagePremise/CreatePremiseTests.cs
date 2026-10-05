using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class CreatePremiseTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresPremiseWithAuditStamp()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);

        var response = await new CreatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidCreateCommand(
                countryId, storeCode: "SC-01"),
                CancellationToken.None);

        Assert.Equal("Seri Rasa Factory", response.Name);
        Assert.Equal("SC-01", response.StoreCode);
        Assert.Equal(PremiseType.Factory, response.PremiseType);
        var stored = await db.Premises.AsNoTracking().SingleAsync();
        Assert.Equal(user.CompanyId, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        Assert.Equal(countryId, stored.CountryId);
    }

    [Fact]
    public async Task Handle_BlankStoreCode_IsStoredAsNull()
    {
        // Whitespace must not become "" - the (CompanyId, StoreCode) unique index would then
        // block a second premise with an absent store code.
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);

        await new CreatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidCreateCommand(countryId, storeCode: "   "),
                CancellationToken.None);
        await new CreatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidCreateCommand(countryId, storeCode: null),
                CancellationToken.None);

        Assert.Equal(2, await db.Premises.CountAsync());
        Assert.Null((await db.Premises.FirstAsync()).StoreCode);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        const string email = "shared@premise.my";
        await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, email: email);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreatePremiseHandler(db, user)
                .Handle(PremiseTestData.ValidCreateCommand(countryId, email: email),
                    CancellationToken.None));

        Assert.Equal("A premise with this e-mail already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_DuplicateStoreCode_ThrowsConflict()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, storeCode: "SC-01");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreatePremiseHandler(db, user)
                .Handle(PremiseTestData.ValidCreateCommand(countryId, storeCode: "SC-01"),
                    CancellationToken.None));

        Assert.Equal("A premise with this store code already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_SoftDeletedPremiseFreesEmailAndStoreCode()
    {
        // Database.md 7: the unique pairs are among live rows - a soft delete frees both
        // (the filtered index and the handler's AnyAsync both skip IsDeleted).
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var oldId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, email: "old@premise.my", storeCode: "SC-01");
        db.Premises.Remove(await db.Premises.SingleAsync(row => row.Id == oldId));
        await db.SaveChangesAsync();

        var response = await new CreatePremiseHandler(db, user)
            .Handle(PremiseTestData.ValidCreateCommand(
                countryId, email: "old@premise.my", storeCode: "SC-01"),
                CancellationToken.None);

        Assert.Equal("old@premise.my", response.Email);
        Assert.Equal("SC-01", response.StoreCode);
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var validator = new CreatePremiseValidator(db, user);

        var noType = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { PremiseType = null });
        var noName = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Name = " " });
        var noEmail = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Email = " " });
        var noAddress1 = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Address1 = " " });
        var noAddress2 = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Address2 = " " });
        var noPostcode = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Postcode = " " });
        var noCountry = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { CountryId = null });
        var noState = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { State = " " });
        var noTelephone = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Telephone = " " });
        var noStatus = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Status = " " });

        Assert.False(noType.IsValid);
        Assert.Contains(noType.Errors,
            failure => failure.ErrorMessage == "Premise type is required.");
        Assert.False(noName.IsValid);
        Assert.Contains(noName.Errors,
            failure => failure.ErrorMessage == "Premise name is required.");
        Assert.False(noEmail.IsValid);
        Assert.Contains(noEmail.Errors,
            failure => failure.ErrorMessage == "Premise email is required.");
        Assert.False(noAddress1.IsValid);
        Assert.Contains(noAddress1.Errors,
            failure => failure.ErrorMessage == "Address line 1 is required.");
        Assert.False(noAddress2.IsValid);
        Assert.Contains(noAddress2.Errors,
            failure => failure.ErrorMessage == "Address line 2 is required.");
        Assert.False(noPostcode.IsValid);
        Assert.Contains(noPostcode.Errors,
            failure => failure.ErrorMessage == "Postcode is required.");
        Assert.False(noCountry.IsValid);
        Assert.Contains(noCountry.Errors,
            failure => failure.ErrorMessage == "Country is required.");
        Assert.False(noState.IsValid);
        Assert.Contains(noState.Errors,
            failure => failure.ErrorMessage == "State is required.");
        Assert.False(noTelephone.IsValid);
        Assert.Contains(noTelephone.Errors,
            failure => failure.ErrorMessage == "Telephone is required.");
        Assert.False(noStatus.IsValid);
        Assert.Contains(noStatus.Errors,
            failure => failure.ErrorMessage == "Status is required.");
    }

    [Fact]
    public async Task Validator_StaleReferences_Fail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var validator = new CreatePremiseValidator(db, user);

        var badManager = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(
                countryId, premiseManagerStaffId: Guid.NewGuid()));
        var badAreaManager = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(
                countryId, areaManagerStaffId: Guid.NewGuid()));
        var badCountry = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { CountryId = Guid.NewGuid() });
        var badPrayerRoom = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(
                countryId, prayerRoomAvailabilityId: Guid.NewGuid()));

        Assert.False(badManager.IsValid);
        Assert.Contains(badManager.Errors,
            failure => failure.ErrorMessage == "Premise manager not found.");
        Assert.False(badAreaManager.IsValid);
        Assert.Contains(badAreaManager.Errors,
            failure => failure.ErrorMessage == "Area manager not found.");
        Assert.False(badCountry.IsValid);
        Assert.Contains(badCountry.Errors,
            failure => failure.ErrorMessage == "Country not found.");
        Assert.False(badPrayerRoom.IsValid);
        Assert.Contains(badPrayerRoom.Errors,
            failure => failure.ErrorMessage == "Prayer room availability not found.");
    }

    [Fact]
    public async Task Validator_PrayerRoomFromAnotherCompany_Fails()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var foreignPrayerRoom = await PremiseTestData.SeedPrayerRoomAsync(
            db, otherCompany.CompanyId);
        var validator = new CreatePremiseValidator(db, user);

        var result = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(
                countryId, prayerRoomAvailabilityId: foreignPrayerRoom));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Prayer room availability not found.");
    }

    [Fact]
    public async Task Validator_LengthLimits_Fail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var validator = new CreatePremiseValidator(db, user);

        var longName = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId, name: new string('x', 201)));
        var longStoreCode = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId, storeCode: new string('x', 51)));
        var longStatus = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId) with { Status = new string('x', 31) });

        Assert.False(longName.IsValid);
        Assert.Contains(longName.Errors,
            failure => failure.ErrorMessage == "Premise name must be 200 characters or fewer.");
        Assert.False(longStoreCode.IsValid);
        Assert.Contains(longStoreCode.Errors,
            failure => failure.ErrorMessage == "Store code must be 50 characters or fewer.");
        Assert.False(longStatus.IsValid);
        Assert.Contains(longStatus.Errors,
            failure => failure.ErrorMessage == "Status must be 30 characters or fewer.");
    }

    [Fact]
    public async Task Validator_InvalidPremiseType_Fails()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var countryId = await PremiseTestData.MalaysiaIdAsync(db);
        var validator = new CreatePremiseValidator(db, user);

        var result = await validator.ValidateAsync(
            PremiseTestData.ValidCreateCommand(countryId)
                with
            { PremiseType = (PremiseType)99 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Premise type is invalid.");
    }
}
