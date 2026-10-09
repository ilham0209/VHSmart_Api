using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditPrefix.AuditPrefixTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class UpdateAuditPrefixTests
{
    private static async Task<UpdateAuditPrefixCommand> ValidCommandAsync(
        TestableVHSmartDbContext db,
        Guid id) =>
        new(
            Id: id,
            BrandId: await SeedBrandAsync(db),
            Prefix: "RTW",
            Description: "Renamed");

    [Fact]
    public async Task Handle_ValidCommand_StoresChangesAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA, prefix: "MRS", description: "Old");
        var command = await ValidCommandAsync(db, id);

        var response = await new UpdateAuditPrefixHandler(db, user)
            .Handle(command, CancellationToken.None);

        var stored = await db.AuditPrefixes.SingleAsync();
        Assert.Equal("RTW", stored.Prefix);
        Assert.Equal("Renamed", stored.Description);
        Assert.Equal(command.BrandId, stored.BrandId);
        // CompanyId is never editable - the row stays with its company (spec 3.3).
        Assert.Equal(CompanyA, stored.CompanyId);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal("RTW", response.Prefix);
    }

    [Fact]
    public async Task Handle_KeepingItsOwnBrand_IsNotAConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var brandId = await SeedBrandAsync(db);
        var id = await SeedPrefixAsync(db, CompanyA, brandId: brandId, prefix: "MRS");

        var response = await new UpdateAuditPrefixHandler(db, user)
            .Handle(
                new UpdateAuditPrefixCommand(id, brandId, "MRS2", null),
                CancellationToken.None);

        Assert.Equal("MRS2", response.Prefix);
    }

    [Fact]
    public async Task Handle_MovingToABrandWithALivePrefix_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var takenBrand = await SeedBrandAsync(db, name: "SERUNAI");
        await SeedPrefixAsync(db, CompanyA, brandId: takenBrand, prefix: "SRN");
        var id = await SeedPrefixAsync(db, CompanyA, prefix: "MRS");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateAuditPrefixHandler(db, user)
                .Handle(
                    new UpdateAuditPrefixCommand(id, takenBrand, "MRS", null),
                    CancellationToken.None));

        Assert.Equal("An audit prefix already exists for this brand.", exception.Message);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await ValidCommandAsync(db, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateAuditPrefixHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedPrefixAsync(db, CompanyB);
        var command = await ValidCommandAsync(db, foreignId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateAuditPrefixHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Validator_ValidCommand_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        var validator = new UpdateAuditPrefixValidator(db, user);

        var result = await validator.ValidateAsync(await ValidCommandAsync(db, id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingId_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db, Guid.Empty);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Id");
    }

    [Fact]
    public async Task Validator_UnknownBrand_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        var validator = new UpdateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db, id) with { BrandId = Guid.NewGuid() };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_MissingPrefix_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        var validator = new UpdateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db, id) with { Prefix = string.Empty };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit prefix is required.");
    }

    [Fact]
    public async Task Validator_PrefixLongerThan20_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        var validator = new UpdateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db, id) with { Prefix = new string('x', 21) };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit prefix must be 20 characters or fewer.");
    }

    [Fact]
    public async Task Validator_DescriptionLongerThan500_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        var validator = new UpdateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db, id) with { Description = new string('x', 501) };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 500 characters or fewer.");
    }
}
