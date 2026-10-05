using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class GetManufacturerSupplierByIdTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ManufacturerSupplierEntity NewRow(Guid companyId) =>
        new()
        {
            CompanyId = companyId,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = "Santan Foods",
            ManufacturerBusinessRegNo = "202301001234",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = "manufacturer@example.com",
            ManufacturerWebpage = "https://santan.example.com",
            SupplierName = "Santan Supplies",
            SupplierAddress = "Jalan Gombak 2",
            SupplierEmail = "supplier@example.com"
        };

    [Fact]
    public async Task Handle_ExistingRow_ReturnsEveryFormField()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA);
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();

        var response = await new GetManufacturerSupplierByIdHandler(db)
            .Handle(new GetManufacturerSupplierByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal(row.Id, response.Id);
        Assert.Equal(ManufacturerSupplierType.Both, response.Type);
        Assert.Equal("202301001234", response.ManufacturerBusinessRegNo);
        Assert.Equal("manufacturer@example.com", response.ManufacturerEmail);
        Assert.Equal("supplier@example.com", response.SupplierEmail);
        Assert.Equal("https://santan.example.com", response.ManufacturerWebpage);
        Assert.Null(response.LogoFileName);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetManufacturerSupplierByIdHandler(db)
                .Handle(new GetManufacturerSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(
            databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB);
        db.ManufacturerSuppliers.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetManufacturerSupplierByIdHandler(db)
                .Handle(new GetManufacturerSupplierByIdQuery(foreignRow.Id), CancellationToken.None));
    }
}
