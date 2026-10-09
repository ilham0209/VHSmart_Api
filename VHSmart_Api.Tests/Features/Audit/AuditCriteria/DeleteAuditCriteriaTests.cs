using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class DeleteAuditCriteriaTests
{
    private static DeleteAuditCriteriaHandler Handler(TestableVHSmartDbContext db) => new(db);

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedCriteriaAsync(db, CompanyA);

        await Handler(db).Handle(
            new DeleteAuditCriteriaCommand(rowId), CancellationToken.None);

        // Soft delete - the interceptor flips the state to Modified with IsDeleted = true
        // (the house pattern); no hard delete ever reaches the store.
        var stored = await db.AuditCriteria.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Equal(rowId, stored.Id);
        Assert.Equal(0, await db.AuditCriteria.CountAsync());
    }

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesItsFindingLinksFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var finding = await SeedFindingAsync(db, CompanyA);
        var rowId = await SeedCriteriaAsync(db, CompanyA);
        await SeedCriteriaFindingLinkAsync(db, CompanyA, rowId, finding);

        // The link rows belong to the criteria: they go before the parent (the
        // Users/DeleteUser order - FK non-nullable + Restrict) or they orphan.
        await Handler(db).Handle(
            new DeleteAuditCriteriaCommand(rowId), CancellationToken.None);

        var links = await db.AuditCriteriaFindings.IgnoreQueryFilters().ToListAsync();
        Assert.All(links, link => Assert.True(link.IsDeleted));
        Assert.True((await db.AuditCriteria.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_UnknownRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        // Unknown or foreign row -> 404 (CodingRules 9), never 403.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db).Handle(
                new DeleteAuditCriteriaCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndKeepsTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategory = await SeedCategoryAsync(db, CompanyB);
        var foreignCriteria = await SeedMasterAsync(db, CompanyB);
        var foreignRow = await SeedCriteriaAsync(
            db, CompanyB, categoryId: foreignCategory, criteriaId: foreignCriteria);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db).Handle(
                new DeleteAuditCriteriaCommand(foreignRow), CancellationToken.None));

        Assert.Equal(1, await db.AuditCriteria.IgnoreQueryFilters().CountAsync(row => !row.IsDeleted));
    }
}
