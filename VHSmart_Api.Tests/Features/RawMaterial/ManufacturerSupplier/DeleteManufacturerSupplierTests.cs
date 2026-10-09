using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class DeleteManufacturerSupplierTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ManufacturerSupplierEntity NewRow(Guid companyId, string email) =>
        new()
        {
            CompanyId = companyId,
            Type = ManufacturerSupplierType.ManufacturerOnly,
            ManufacturerName = "Santan Foods",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = email
        };

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesAndFreesTheEmail()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "manufacturer@example.com");
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();

        await new DeleteManufacturerSupplierHandler(db)
            .Handle(new DeleteManufacturerSupplierCommand(row.Id), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the global filter hides it.
        var stored = await db.ManufacturerSuppliers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.ManufacturerSuppliers.ToArrayAsync());

        // The e-mail is free again for a new live row (spec 21.9).
        Assert.False(await db.ManufacturerSuppliers.AnyAsync(
            maker => maker.ManufacturerEmail == "manufacturer@example.com"));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteManufacturerSupplierHandler(db)
                .Handle(new DeleteManufacturerSupplierCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowUsedByARawMaterial_ThrowsBusinessRule()
    {
        var user = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA);
        var db = await CreateDbAsync(user);
        var row = NewRow(CompanyA, "manufacturer@example.com");
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        // RawMaterials (RM-02) now exists, so the guard deferred in RM-01 can run.
        db.RawMaterials.Add(new RawMaterialEntity
        {
            CompanyId = CompanyA,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = Guid.NewGuid(),
            Ingredient = "Rice Flour",
            IngredientCode = "RM-001",
            ManufacturerSupplierId = row.Id
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new DeleteManufacturerSupplierHandler(db)
                .Handle(new DeleteManufacturerSupplierCommand(row.Id), CancellationToken.None));

        Assert.Equal("This manufacturer and supplier is used by a raw material.", exception.Message);
        Assert.False((await db.ManufacturerSuppliers.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(
            databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "b@example.com");
        db.ManufacturerSuppliers.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteManufacturerSupplierHandler(db)
                .Handle(new DeleteManufacturerSupplierCommand(foreignRow.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "manufacturer@example.com");
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();

        await new DeleteManufacturerSupplierHandler(db)
            .Handle(new DeleteManufacturerSupplierCommand(row.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteManufacturerSupplierHandler(db)
                .Handle(new DeleteManufacturerSupplierCommand(row.Id), CancellationToken.None));
    }

    // The PrdProducts and AppBatches parts RM-01 deferred to PD-01 / HA-01, landed here.
    [Fact]
    public async Task Handle_RowUsedByAProduct_ThrowsBusinessRule()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "manufacturer@example.com");
        db.ManufacturerSuppliers.Add(row);
        var schemeId = await db.Schemes.AsNoTracking()
            .OrderBy(scheme => scheme.SortOrder)
            .Select(scheme => scheme.Id)
            .FirstAsync();
        db.Products.Add(new ProductEntity
        {
            CompanyId = CompanyA,
            SchemeId = schemeId,
            Name = "Santan Kicap",
            Code = "PRD-001",
            ManufacturerSupplierId = row.Id
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new DeleteManufacturerSupplierHandler(db)
                .Handle(new DeleteManufacturerSupplierCommand(row.Id), CancellationToken.None));

        Assert.Equal(
            "This manufacturer and supplier is used by a product.", exception.Message);
        Assert.False(
            (await db.ManufacturerSuppliers.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_RowUsedByABatch_ThrowsBusinessRule()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "manufacturer@example.com");
        db.ManufacturerSuppliers.Add(row);
        var schemeId = await db.Schemes.AsNoTracking()
            .OrderBy(scheme => scheme.SortOrder)
            .Select(scheme => scheme.Id)
            .FirstAsync();
        db.Batches.Add(new BatchEntity
        {
            CompanyId = CompanyA,
            SchemeId = schemeId,
            Name = "Santan Batch Pertama",
            ManufacturerSupplierId = row.Id
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new DeleteManufacturerSupplierHandler(db)
                .Handle(new DeleteManufacturerSupplierCommand(row.Id), CancellationToken.None));

        Assert.Equal("This manufacturer and supplier is used by a batch.", exception.Message);
        Assert.False(
            (await db.ManufacturerSuppliers.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }
}
