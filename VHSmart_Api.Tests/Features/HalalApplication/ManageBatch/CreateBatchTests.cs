using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class CreateBatchTests
{
    private static async Task<CreateBatchCommand> ValidCommandAsync(TestableVHSmartDbContext db) =>
        new(
            SchemeId: await ProductSchemeIdAsync(db),
            Name: "Santan Batch Pertama",
            CbReferenceNo: "CB-REF-1",
            SubmissionPlannedDate: new DateTime(2026, 11, 27),
            BrandId: await SeedBrandAsync(db),
            Description: "First batch");

    [Fact]
    public async Task Handle_ValidCommand_CreatesRowForTheJwtCompanyAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await ValidCommandAsync(db);

        var response = await new CreateBatchHandler(db, user)
            .Handle(command, CancellationToken.None);

        var stored = await db.Batches.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(command.SchemeId, stored.SchemeId);
        Assert.Equal(command.Name, stored.Name);
        Assert.Equal(command.CbReferenceNo, stored.CbReferenceNo);
        Assert.Equal(command.SubmissionPlannedDate, stored.SubmissionPlannedDate);
        Assert.Equal(command.BrandId, stored.BrandId);
        Assert.Equal(command.Description, stored.Description);

        // The add modal has no manufacturer slot (spec 12.2) - edit adds it.
        Assert.Null(stored.ManufacturerSupplierId);

        Assert.Equal(stored.Id, response.Id);
        Assert.False(response.IsFoodPremiseScheme);
        Assert.Empty(response.Products);
        Assert.Empty(response.Premises);
        Assert.Equal(stored.SysDateCreated, response.CreatedDate);
    }

    [Fact]
    public async Task Handle_FoodPremiseScheme_MarksTheResponse()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = (await ValidCommandAsync(db)) with
        {
            SchemeId = await FoodPremiseSchemeIdAsync(db)
        };

        var response = await new CreateBatchHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.True(response.IsFoodPremiseScheme);
    }

    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflictWithTheLegacyMessage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBatchAsync(db, CompanyA, name: "Santan Batch Pertama");
        var command = await ValidCommandAsync(db);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateBatchHandler(db, user)
                .Handle(command, CancellationToken.None));

        Assert.Equal("Error! Please provide unique batch name", exception.Message);
        Assert.Equal(1, await db.Batches.CountAsync());
    }

    [Fact]
    public async Task Handle_DuplicateNameOfAnotherCompany_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBatchAsync(db, CompanyB, name: "Santan Batch Pertama");
        var command = await ValidCommandAsync(db);

        var response = await new CreateBatchHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Santan Batch Pertama", response.Name);

        // The company filter only shows the caller's own row; the foreign row is still there.
        Assert.Equal(1, await db.Batches.CountAsync());
        Assert.Equal(2, await db.Batches.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_ValidCommand_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);

        var result = await validator.ValidateAsync(await ValidCommandAsync(db));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingScheme_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with { SchemeId = Guid.Empty };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Scheme is required.");
    }

    [Fact]
    public async Task Validator_UnknownScheme_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with { SchemeId = Guid.NewGuid() };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Scheme not found.");
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with { Name = string.Empty };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Batch name is required.");
    }

    [Fact]
    public async Task Validator_NameLongerThan200_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with { Name = new string('x', 201) };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Batch name must be 200 characters or fewer.");
    }

    // The legacy rule "Brand Owner is required in every case" (spec 12.2 [CODE]).
    [Fact]
    public async Task Validator_MissingBrandOwner_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with { BrandId = null };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand owner is required.");
    }

    [Fact]
    public async Task Validator_BrandOwnerOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var foreignBrand = await SeedBrandAsync(db, companyId: CompanyB);
        var command = await ValidCommandAsync(db) with { BrandId = foreignBrand };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand owner not found.");
    }

    [Fact]
    public async Task Validator_DescriptionLongerThan1000_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with
        {
            Description = new string('x', 1001)
        };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_CbReferenceLongerThan100_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateBatchValidator(db, user);
        var command = await ValidCommandAsync(db) with
        {
            CbReferenceNo = new string('x', 101)
        };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "CB reference number must be 100 characters or fewer.");
    }
}
