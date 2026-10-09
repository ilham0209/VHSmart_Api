using VHSmart_Api.Features.Audit.AuditChecklist;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class GetAuditChecklistOptionsTests
{
    private static GetAuditChecklistOptionsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ReturnsInternalAuditCategoriesSortedByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedChecklistCategoryAsync(db, name: "Syariah");
        await SeedChecklistCategoryAsync(db, name: "Internal Supplier");

        var options = await Handler(db, user).Handle(
            new GetAuditChecklistOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Internal Supplier", "Syariah" },
            options.Categories.Select(row => row.Name));
    }

    [Fact]
    public async Task Handle_ExcludesExternalCategoryAndOtherGroups()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedChecklistCategoryAsync(db, name: "Syariah");
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
            new GetAuditChecklistOptionsQuery(), CancellationToken.None);

        // Group AUDIT / "Internal - Audit Category" only (§14.1 probable mapping [VERIFY],
        // flagged) - the External pair and other groups stay out.
        var category = Assert.Single(options.Categories);
        Assert.Equal("Syariah", category.Name);
    }

    [Fact]
    public async Task Handle_RowsOfAnotherCompany_AreInvisible()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedChecklistCategoryAsync(db, CompanyB, name: "Foreign category");

        var options = await Handler(db, user).Handle(
            new GetAuditChecklistOptionsQuery(), CancellationToken.None);

        Assert.Empty(options.Categories);
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var options = await Handler(db, user).Handle(
            new GetAuditChecklistOptionsQuery(), CancellationToken.None);

        Assert.Empty(options.Categories);
    }
}
