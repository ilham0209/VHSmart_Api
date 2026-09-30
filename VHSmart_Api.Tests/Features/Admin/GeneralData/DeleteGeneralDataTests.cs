using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class DeleteGeneralDataTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<GeneralDataEntity> SeedRowAsync(
        TestableVHSmartDbContext db,
        GeneralDataGroup group,
        string category,
        string name,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = group,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesAndFreesTheName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Amanah");

        await new DeleteGeneralDataHandler(db)
            .Handle(new DeleteGeneralDataCommand(row.Id), CancellationToken.None);

        // Soft delete: the row stays, marked deleted, and leaves the filtered list.
        var stored = await db.GeneralData
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == row.Id);
        Assert.True(stored.IsDeleted);

        // The unique slot is free again: a new row with the same name succeeds.
        var replacement = await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Amanah");
        Assert.Equal(CompanyA, replacement.CompanyId);
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Amanah");
        await new DeleteGeneralDataHandler(db)
            .Handle(new DeleteGeneralDataCommand(row.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteGeneralDataHandler(db)
                .Handle(new DeleteGeneralDataCommand(row.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteGeneralDataHandler(db)
                .Handle(new DeleteGeneralDataCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignRow = await SeedRowAsync(
            db, GeneralDataGroup.COMPANY, "Brand", "Foreign", companyId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteGeneralDataHandler(db)
                .Handle(new DeleteGeneralDataCommand(foreignRow.Id), CancellationToken.None));
    }
}
