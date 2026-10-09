using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditPrefix.AuditPrefixTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class CreateAuditPrefixTests
{
    private static async Task<CreateAuditPrefixCommand> ValidCommandAsync(
        TestableVHSmartDbContext db) =>
        new(
            BrandId: await SeedBrandAsync(db),
            Prefix: "MRS",
            Description: "Natural brand audit prefix");

    [Fact]
    public async Task Handle_ValidCommand_CreatesRowForTheJwtCompanyAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await ValidCommandAsync(db);

        var response = await new CreateAuditPrefixHandler(db, user)
            .Handle(command, CancellationToken.None);

        var stored = await db.AuditPrefixes.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(command.BrandId, stored.BrandId);
        Assert.Equal("MRS", stored.Prefix);
        Assert.Equal("Natural brand audit prefix", stored.Description);
        Assert.False(stored.IsDeleted);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal(command.BrandId, response.BrandId);
        Assert.Equal("MRS", response.Prefix);
        Assert.Equal("Natural brand audit prefix", response.Description);
    }

    [Fact]
    public async Task Handle_DuplicateBrandInSameCompany_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var brandId = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brandId: brandId, prefix: "MRS");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateAuditPrefixHandler(db, user)
                .Handle(
                    new CreateAuditPrefixCommand(brandId, "SRN", null),
                    CancellationToken.None));

        Assert.Equal("An audit prefix already exists for this brand.", exception.Message);
        Assert.Equal(1, await db.AuditPrefixes.CountAsync());
    }

    [Fact]
    public async Task Handle_AnotherBrandInSameCompany_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedPrefixAsync(db, CompanyA, prefix: "MRS");

        var response = await new CreateAuditPrefixHandler(db, user)
            .Handle(
                new CreateAuditPrefixCommand(await SeedBrandAsync(db, name: "SERUNAI"), "SRN", null),
                CancellationToken.None);

        Assert.Equal("SRN", response.Prefix);
        Assert.Equal(2, await db.AuditPrefixes.CountAsync());
    }

    [Fact]
    public async Task Validator_ValidCommand_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditPrefixValidator(db, user);

        var result = await validator.ValidateAsync(await ValidCommandAsync(db));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingBrand_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db) with { BrandId = Guid.Empty };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand is required.");
    }

    [Fact]
    public async Task Validator_UnknownBrand_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db) with { BrandId = Guid.NewGuid() };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_BrandOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditPrefixValidator(db, user);
        var foreignBrand = await SeedBrandAsync(db, companyId: CompanyB);
        var command = await ValidCommandAsync(db) with { BrandId = foreignBrand };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_NonBrandGeneralData_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditPrefixValidator(db, user);
        var nonBrand = await SeedNonBrandGeneralDataAsync(db);
        var command = await ValidCommandAsync(db) with { BrandId = nonBrand };

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
        var validator = new CreateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db) with { Prefix = string.Empty };

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
        var validator = new CreateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db) with { Prefix = new string('x', 21) };

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
        var validator = new CreateAuditPrefixValidator(db, user);
        var command = await ValidCommandAsync(db) with { Description = new string('x', 501) };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 500 characters or fewer.");
    }
}
