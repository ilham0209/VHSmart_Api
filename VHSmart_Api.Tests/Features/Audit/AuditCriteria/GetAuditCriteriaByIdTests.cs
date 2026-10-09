using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class GetAuditCriteriaByIdTests
{
    private static GetAuditCriteriaByIdHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ExistingRow_ReturnsTheModalDetailWithLiveFindingIds()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db, name: "Storage");
        var criteria = await SeedMasterAsync(db, CompanyA, text: "Temperature control");
        var subCriteria = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Chiller logs");
        var finding = await SeedFindingAsync(db, CompanyA);
        var rowId = await SeedCriteriaAsync(
            db, CompanyA,
            categoryId: category,
            criteriaId: criteria,
            subCriteriaId: subCriteria,
            referenceCategory: "ISO 22000",
            reference: "Clause 8",
            potentialPoint: 4m,
            description: "Daily logs kept");
        await SeedCriteriaFindingLinkAsync(db, CompanyA, rowId, finding);

        var response = await Handler(db, user).Handle(
            new GetAuditCriteriaByIdQuery(rowId), CancellationToken.None);

        Assert.Equal(rowId, response.Id);
        Assert.Equal(category, response.CategoryId);
        Assert.Equal(criteria, response.CriteriaId);
        Assert.Equal(subCriteria, response.SubCriteriaId);
        Assert.Equal("ISO 22000", response.ReferenceCategory);
        Assert.Equal("Clause 8", response.Reference);
        Assert.Equal(4m, response.PotentialPoint);
        Assert.Equal([finding], response.Findings);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new GetAuditCriteriaByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignRow = await SeedCriteriaAsync(db, CompanyB);

        // Unknown AND foreign both answer 404 - the client must not learn that the id
        // exists elsewhere (CodingRules 9).
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new GetAuditCriteriaByIdQuery(foreignRow), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedCriteriaAsync(db, CompanyA);
        db.AuditCriteria.Remove(
            await db.AuditCriteria.SingleAsync());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new GetAuditCriteriaByIdQuery(rowId), CancellationToken.None));
    }
}
