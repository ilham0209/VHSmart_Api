using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class GetAllManufacturerSuppliersTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ManufacturerSupplierEntity NewRow(
        Guid companyId,
        string manufacturerName,
        string? supplierName = null,
        string? email = null) =>
        new()
        {
            CompanyId = companyId,
            Type = supplierName is null
                ? ManufacturerSupplierType.ManufacturerOnly
                : ManufacturerSupplierType.Both,
            ManufacturerName = manufacturerName,
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerCountryId = Guid.NewGuid(),
            ManufacturerEmail = email ?? $"{manufacturerName.Replace(' ', '-').ToLowerInvariant()}@example.com",
            SupplierName = supplierName,
            SupplierAddress = supplierName is null ? null : "Jalan Gombak 2",
            SupplierCountryId = supplierName is null ? null : Guid.NewGuid(),
            SupplierEmail = supplierName is null ? null : $"supply-{manufacturerName.Replace(' ', '-').ToLowerInvariant()}@example.com"
        };

    [Fact]
    public async Task Handle_SearchTerm_FindsManufacturerOrSupplierColumns()
    {
        var db = await CreateDbAsync(UserA());
        db.ManufacturerSuppliers.AddRange(
            NewRow(CompanyA, "Santan Foods", "Santan Supplies"),
            NewRow(CompanyA, "Maybank Kitchen"),
            NewRow(CompanyA, "CIMB Catering", "CIMB Distribution"));
        await db.SaveChangesAsync();

        var byManufacturer = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(
                new GetAllManufacturerSuppliersQuery { Request = new DataGridRequest { SearchTerm = "Santan" } },
                CancellationToken.None);
        var bySupplier = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(
                new GetAllManufacturerSuppliersQuery { Request = new DataGridRequest { SearchTerm = "CIMB Dist" } },
                CancellationToken.None);

        Assert.Equal("Santan Foods", Assert.Single(byManufacturer.Data).ManufacturerName);
        Assert.Equal("CIMB Catering", Assert.Single(bySupplier.Data).ManufacturerName);
    }

    [Fact]
    public async Task Handle_UnusedHalf_ReturnsNullsForTheClientToShowAsNotApplicable()
    {
        var db = await CreateDbAsync(UserA());
        db.ManufacturerSuppliers.Add(NewRow(CompanyA, "Santan Foods"));
        await db.SaveChangesAsync();

        var result = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(new GetAllManufacturerSuppliersQuery(), CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal("Santan Foods", row.ManufacturerName);
        // Spec 10.1: the absent half is displayed as "N/A"; the API returns null for it.
        Assert.Null(row.SupplierName);
        Assert.Null(row.SupplierAddress);
        Assert.Null(row.SupplierEmail);
        Assert.Null(row.SupplierContactNo);
    }

    [Fact]
    public async Task Handle_Category_ShowsTheManufacturerTypeName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var manufacturerType = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Manufacturer Type",
            Name = "Local Producer"
        };
        db.GeneralData.Add(manufacturerType);
        var row = NewRow(CompanyA, "Santan Foods");
        row.ManufacturerTypeId = manufacturerType.Id;
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();

        var result = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(new GetAllManufacturerSuppliersQuery(), CancellationToken.None);

        Assert.Equal("Local Producer", Assert.Single(result.Data).Category);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        db.ManufacturerSuppliers.AddRange(
            NewRow(CompanyA, "Company A maker"),
            NewRow(CompanyB, "Company B maker"));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(new GetAllManufacturerSuppliersQuery(), CancellationToken.None);

        Assert.Equal(1, asCompanyA.TotalRecords);
        Assert.Equal("Company A maker", Assert.Single(asCompanyA.Data).ManufacturerName);

        var dbB = TestDbFactory.Create(databaseName, UserB());
        var asCompanyB = await new GetAllManufacturerSuppliersHandler(dbB)
            .Handle(new GetAllManufacturerSuppliersQuery(), CancellationToken.None);

        Assert.Equal(1, asCompanyB.TotalRecords);
        Assert.Equal("Company B maker", Assert.Single(asCompanyB.Data).ManufacturerName);
    }

    [Fact]
    public async Task Handle_SortBySysDateModified_NewestFirst()
    {
        var db = await CreateDbAsync(UserA());
        var older = NewRow(CompanyA, "Older maker");
        older.SysDateModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = NewRow(CompanyA, "Newer maker");
        newer.SysDateModified = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        db.ManufacturerSuppliers.AddRange(older, newer);
        await db.SaveChangesAsync();

        var result = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(
                new GetAllManufacturerSuppliersQuery
                {
                    Request = new DataGridRequest
                    {
                        SortBy = "SysDateModified",
                        SortDescending = true
                    }
                },
                CancellationToken.None);

        Assert.Equal(
            new string?[] { "Newer maker", "Older maker" },
            result.Data.Select(row => row.ManufacturerName).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        var row = NewRow(CompanyA, "Doomed maker");
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        db.ManufacturerSuppliers.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllManufacturerSuppliersHandler(db)
            .Handle(new GetAllManufacturerSuppliersQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }
}
