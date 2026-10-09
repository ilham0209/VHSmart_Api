using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class GetAuditChecklistByIdTests
{
    private static GetAuditChecklistByIdHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ExistingRow_ReturnsHeaderAndLiveCriteriaIds()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, name: "Syariah");
        var criteria1 = await SeedCriteriaAsync(db, CompanyA, criteriaText: "Alfa rule");
        var criteria2 = await SeedCriteriaAsync(db, CompanyA, criteriaText: "Zulu rule");
        var rowId = await SeedChecklistAsync(
            db, CompanyA, categoryId: category,
            name: "Checklist testing 18 Nov 25", description: "Do not use");
        await SeedChecklistLinkAsync(db, CompanyA, rowId, criteria2);
        await SeedChecklistLinkAsync(db, CompanyA, rowId, criteria1);

        var response = await Handler(db, user).Handle(
            new GetAuditChecklistByIdQuery(rowId), CancellationToken.None);

        Assert.Equal(rowId, response.Id);
        Assert.Equal(category, response.ChecklistCategoryId);
        Assert.Equal("Checklist testing 18 Nov 25", response.Name);
        Assert.Equal("Do not use", response.Description);
        // The preselected ticks of the Criteria Selection table (order-independent: the
        // handler returns them in stable AuditCriteriaId order).
        Assert.Equal(2, response.CriteriaIds.Count);
        Assert.Contains(criteria1, response.CriteriaIds);
        Assert.Contains(criteria2, response.CriteriaIds);
    }

    [Fact]
    public async Task Handle_ChecklistUsedByAPlan_StillReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedChecklistAsync(db, CompanyA);
        await SeedPlanAsync(db, CompanyA, rowId);

        // The lock freezes edit and delete only - view keeps working (spec 14.6).
        var response = await Handler(db, user).Handle(
            new GetAuditChecklistByIdQuery(rowId), CancellationToken.None);

        Assert.Equal(rowId, response.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new GetAuditChecklistByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignRow = await SeedChecklistAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new GetAuditChecklistByIdQuery(foreignRow), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedChecklistAsync(db, CompanyA);
        db.AuditChecklists.Remove(await db.AuditChecklists.SingleAsync());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new GetAuditChecklistByIdQuery(rowId), CancellationToken.None));
    }
}
