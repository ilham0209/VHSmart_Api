using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditPrefix.AuditPrefixTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class DeleteAuditPrefixTests
{
    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);

        await new DeleteAuditPrefixHandler(db).Handle(
            new DeleteAuditPrefixCommand(id), CancellationToken.None);

        // Soft delete only - no physical DELETE (CodingRules 7.1).
        var stored = await db.AuditPrefixes.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == id);
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.AuditPrefixes.ToListAsync());
    }

    [Fact]
    public async Task Handle_AfterDelete_TheSameBrandSlotIsFreeAgain()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var brandId = await SeedBrandAsync(db);
        var id = await SeedPrefixAsync(db, CompanyA, brandId: brandId);
        await new DeleteAuditPrefixHandler(db).Handle(
            new DeleteAuditPrefixCommand(id), CancellationToken.None);

        var response = await new CreateAuditPrefixHandler(db, user)
            .Handle(
                new CreateAuditPrefixCommand(brandId, "MRS", "recreated"),
                CancellationToken.None);

        Assert.Equal("MRS", response.Prefix);
        Assert.Equal(1, await db.AuditPrefixes.CountAsync());
        Assert.Equal(2, await db.AuditPrefixes.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteAuditPrefixHandler(db).Handle(
                new DeleteAuditPrefixCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedPrefixAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteAuditPrefixHandler(db).Handle(
                new DeleteAuditPrefixCommand(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        await new DeleteAuditPrefixHandler(db).Handle(
            new DeleteAuditPrefixCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteAuditPrefixHandler(db).Handle(
                new DeleteAuditPrefixCommand(id), CancellationToken.None));
    }
}
