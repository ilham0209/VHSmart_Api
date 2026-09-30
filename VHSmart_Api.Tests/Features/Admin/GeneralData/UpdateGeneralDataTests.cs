using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class UpdateGeneralDataTests
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
    public async Task Handle_ValidCommand_UpdatesRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Old name");

        var response = await new UpdateGeneralDataHandler(db).Handle(
            new UpdateGeneralDataCommand(
                row.Id, GeneralDataGroup.COMPANY, "Ownership Type", "New name", "Updated"),
            CancellationToken.None);

        Assert.Equal(row.Id, response.Id);
        Assert.Equal("New name", response.Name);

        var stored = await db.GeneralData.AsNoTracking().SingleAsync();
        Assert.Equal(GeneralDataGroup.COMPANY, stored.Group);
        Assert.Equal("Ownership Type", stored.Category);
        Assert.Equal("New name", stored.Name);
        Assert.Equal("Updated", stored.Description);
        // The row stays with its company - CompanyId is not part of the command.
        Assert.Equal(CompanyA, stored.CompanyId);
    }

    [Fact]
    public async Task Handle_DuplicateOfAnotherRow_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Amanah");
        var second = await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Zafran");

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateGeneralDataHandler(db).Handle(
                new UpdateGeneralDataCommand(
                    second.Id, GeneralDataGroup.COMPANY, "Brand", "Amanah", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DuplicateOfItself_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, GeneralDataGroup.COMPANY, "Brand", "Amanah");

        var response = await new UpdateGeneralDataHandler(db).Handle(
            new UpdateGeneralDataCommand(
                row.Id, GeneralDataGroup.COMPANY, "Brand", "Amanah", "A description"),
            CancellationToken.None);

        Assert.Equal("A description", response.Description);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateGeneralDataHandler(db).Handle(
                new UpdateGeneralDataCommand(
                    Guid.NewGuid(), GeneralDataGroup.COMPANY, "Brand", "Name", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignRow = await SeedRowAsync(
            db, GeneralDataGroup.COMPANY, "Brand", "Foreign", companyId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateGeneralDataHandler(db).Handle(
                new UpdateGeneralDataCommand(
                    foreignRow.Id, GeneralDataGroup.COMPANY, "Brand", "Renamed", null),
                CancellationToken.None));
    }
}
