using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class UpdateBatchTests
{
    private static async Task<(TestableVHSmartDbContext db, TestCurrentUser user, Guid Id)>
        SeededBatchAsync()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedBatchAsync(
            db, CompanyA, name: "Santan Batch Pertama", cbReferenceNo: "CB-REF-1");
        return (db, user, id);
    }

    private static async Task<UpdateBatchCommand> ValidCommandAsync(
        TestableVHSmartDbContext db,
        Guid id,
        Guid? schemeId = null,
        Guid? manufacturerId = null) =>
        new(
            Id: id,
            SchemeId: schemeId ?? await ProductSchemeIdAsync(db),
            Name: "Batch 271125/2",
            CbReferenceNo: "CB-REF-2",
            SubmissionPlannedDate: new DateTime(2026, 11, 27),
            BrandId: await SeedBrandAsync(db, name: "SERUNAI"),
            ManufacturerSupplierId: manufacturerId ?? await SeedManufacturerAsync(db),
            Description: "Edited");

    [Fact]
    public async Task Handle_ValidCommand_UpdatesEveryFormField()
    {
        var (db, user, id) = await SeededBatchAsync();
        var command = await ValidCommandAsync(db, id);

        var response = await new UpdateBatchHandler(db, user)
            .Handle(command, CancellationToken.None);

        var stored = await db.Batches.SingleAsync();
        Assert.Equal(command.Name, stored.Name);
        Assert.Equal(command.CbReferenceNo, stored.CbReferenceNo);
        Assert.Equal(command.SubmissionPlannedDate, stored.SubmissionPlannedDate);
        Assert.Equal(command.BrandId, stored.BrandId);
        Assert.Equal(command.ManufacturerSupplierId, stored.ManufacturerSupplierId);
        Assert.Equal(command.Description, stored.Description);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal(command.Name, response.Name);
    }

    [Fact]
    public async Task Handle_KeepingItsOwnName_IsNotADuplicate()
    {
        var (db, user, id) = await SeededBatchAsync();
        var command = await ValidCommandAsync(db, id);
        command = command with { Name = "Santan Batch Pertama" };

        var response = await new UpdateBatchHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Santan Batch Pertama", response.Name);
    }

    [Fact]
    public async Task Handle_DuplicateNameOfAnotherRow_ThrowsConflictWithTheLegacyMessage()
    {
        var (db, user, id) = await SeededBatchAsync();
        await SeedBatchAsync(db, CompanyA, name: "Batch 271125/2");
        var command = await ValidCommandAsync(db, id);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateBatchHandler(db, user)
                .Handle(command, CancellationToken.None));

        Assert.Equal("Error! Please provide unique batch name", exception.Message);
        var stored = await db.Batches.SingleAsync(row => row.Id == id);
        Assert.Equal("Santan Batch Pertama", stored.Name);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var (db, user, _) = await SeededBatchAsync();
        var command = await ValidCommandAsync(db, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateBatchHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndKeepsTheRow()
    {
        var (db, user, _) = await SeededBatchAsync();
        var foreignId = await SeedBatchAsync(db, CompanyB, name: "Foreign Batch");
        var command = await ValidCommandAsync(db, foreignId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateBatchHandler(db, user)
                .Handle(command, CancellationToken.None));

        var stored = await db.Batches
            .IgnoreQueryFilters()
            .SingleAsync(row => row.Id == foreignId);
        Assert.Equal("Foreign Batch", stored.Name);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Validator_ProductSchemeWithoutManufacturer_Fails()
    {
        var (db, user, id) = await SeededBatchAsync();
        var validator = new UpdateBatchValidator(db, user);
        var command = await ValidCommandAsync(db, id);
        command = command with { ManufacturerSupplierId = null };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer is required.");
    }

    // Spec 12.2 flow 6: the Food Premise form names brand + company only, never a manufacturer.
    [Fact]
    public async Task Validator_FoodPremiseSchemeWithoutManufacturer_Passes()
    {
        var (db, user, id) = await SeededBatchAsync();
        var validator = new UpdateBatchValidator(db, user);
        var command = await ValidCommandAsync(db, id);
        command = command with
        {
            SchemeId = await FoodPremiseSchemeIdAsync(db),
            ManufacturerSupplierId = null
        };

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_UnknownManufacturer_Fails()
    {
        var (db, user, id) = await SeededBatchAsync();
        var validator = new UpdateBatchValidator(db, user);
        var command = await ValidCommandAsync(db, id);
        command = command with { ManufacturerSupplierId = Guid.NewGuid() };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_ManufacturerOfAnotherCompany_Fails()
    {
        var (db, user, id) = await SeededBatchAsync();
        var validator = new UpdateBatchValidator(db, user);
        var foreign = await SeedManufacturerAsync(db, companyId: CompanyB);
        var command = await ValidCommandAsync(db, id);
        command = command with { ManufacturerSupplierId = foreign };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_SupplierOnlyRow_IsNotAManufacturer()
    {
        var (db, user, id) = await SeededBatchAsync();
        var supplierOnly = new ManufacturerSupplierEntity
        {
            CompanyId = CompanyA,
            Type = ManufacturerSupplierType.SupplierOnly,
            SupplierName = "Supply Sdn Bhd",
            SupplierAddress = "Jalan Supply 1",
            SupplierEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(supplierOnly);
        await db.SaveChangesAsync();

        var validator = new UpdateBatchValidator(db, user);
        var command = await ValidCommandAsync(db, id);
        command = command with { ManufacturerSupplierId = supplierOnly.Id };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_MissingBrandOwner_Fails()
    {
        var (db, user, id) = await SeededBatchAsync();
        var validator = new UpdateBatchValidator(db, user);
        var command = await ValidCommandAsync(db, id);
        command = command with { BrandId = null };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand owner is required.");
    }
}
