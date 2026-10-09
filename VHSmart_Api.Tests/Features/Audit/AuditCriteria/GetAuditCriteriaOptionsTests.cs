using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class GetAuditCriteriaOptionsTests
{
    private static GetAuditCriteriaOptionsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ReturnsCategoriesCriteriaSubCriteriaAndFindings()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCategoryAsync(db, name: "Zulu category");
        await SeedCategoryAsync(db, name: "Alfa category");
        await SeedMasterAsync(db, CompanyA, text: "Zulu criteria");
        await SeedMasterAsync(db, CompanyA, text: "Alfa criteria");
        await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Chiller logs");
        await SeedFindingAsync(db, CompanyA, name: "Finding B", findingCode: "FB");
        await SeedFindingAsync(db, CompanyA, name: "Finding A", findingCode: "FA");

        var options = await Handler(db, user).Handle(
            new GetAuditCriteriaOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alfa category", "Zulu category" },
            options.Categories.Select(row => row.Name));
        Assert.Equal(
            new[] { "Alfa criteria", "Zulu criteria" },
            options.Criteria.Select(row => row.Text));
        Assert.Equal("Chiller logs", Assert.Single(options.SubCriteria).Text);
        Assert.Equal(
            new[] { "Finding A", "Finding B" },
            options.Findings.Select(row => row.Name));
        Assert.Equal(
            new[] { "FA", "FB" },
            options.Findings.Select(row => row.FindingCode));
    }

    [Fact]
    public async Task Handle_CategoriesOnlyTheInternalAuditCategoryPair()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCategoryAsync(db, name: "Kitchen");
        await SeedExternalCategoryAsync(db);
        var wrongGroup = new VHSmart_Api.Shared.Domain.Admin.GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = VHSmart_Api.Shared.Domain.Admin.GeneralDataGroup.COMPANY,
            Category = "Ownership Type",
            Name = "Sole Proprietor"
        };
        db.GeneralData.Add(wrongGroup);
        await db.SaveChangesAsync();

        var options = await Handler(db, user).Handle(
            new GetAuditCriteriaOptionsQuery(), CancellationToken.None);

        // Category dropdown = Group AUDIT / "Internal - Audit Category" only (the 14.1
        // probable mapping, [VERIFY] question 36 - flagged); the External pair of the same
        // group and other groups stay out.
        var category = Assert.Single(options.Categories);
        Assert.Equal("Kitchen", category.Name);
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCategoryAsync(db, CompanyB, name: "Foreign category");
        await SeedMasterAsync(db, CompanyB, text: "Foreign criteria");
        await SeedMasterAsync(
            db, CompanyB, AuditCriteriaMasterKind.SubCriteria, "Foreign sub");
        await SeedFindingAsync(db, CompanyB, name: "Foreign finding");

        var options = await Handler(db, user).Handle(
            new GetAuditCriteriaOptionsQuery(), CancellationToken.None);

        // Every list is explicitly company-scoped so a ViewAll token must not read
        // another tenant's data (the GetAuditPrefixOptions reasoning).
        Assert.Empty(options.Categories);
        Assert.Empty(options.Criteria);
        Assert.Empty(options.SubCriteria);
        Assert.Empty(options.Findings);
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyLists()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var options = await Handler(db, user).Handle(
            new GetAuditCriteriaOptionsQuery(), CancellationToken.None);

        Assert.Empty(options.Categories);
        Assert.Empty(options.Criteria);
        Assert.Empty(options.SubCriteria);
        Assert.Empty(options.Findings);
    }
}
