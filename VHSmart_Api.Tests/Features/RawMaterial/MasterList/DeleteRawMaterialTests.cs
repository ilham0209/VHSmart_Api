using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class DeleteRawMaterialTests
{
    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesAndFreesTheCode()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db, accessibleCompanyIds: [await SeedCompanyAsync(db, "Partner")]);

        // A real delete request starts from a fresh scope, where only the loaded row is tracked;
        // the sharing rows this test seeded in the same context are detached so EF does not try
        // to sever the required relationship when the row goes.
        db.ChangeTracker.Clear();

        await new DeleteRawMaterialHandler(db, user)
            .Handle(new DeleteRawMaterialCommand(id), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the global filter hides it.
        var stored = await db.RawMaterials.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.RawMaterials.ToArrayAsync());

        // D-17: the code is free again for a new live row.
        Assert.False(await db.RawMaterials.AnyAsync(row => row.IngredientCode == "RM-001"));
    }

    [Fact]
    public async Task Handle_ExistingRow_KeepsItsAccessibleForRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var companyId = await SeedCompanyAsync(db, "Partner");
        var id = await SeedRowAsync(db, accessibleCompanyIds: [companyId]);

        // Detach the sharing rows seeded above: a delete request only ever loads the row itself.
        db.ChangeTracker.Clear();

        await new DeleteRawMaterialHandler(db, user)
            .Handle(new DeleteRawMaterialCommand(id), CancellationToken.None);

        // Like every other child list here (P-01, CI-03): the sharing rows belong to the raw
        // material and are not deleted with it.
        var sharing = await db.RawMaterialAccessibleCompanies.AsNoTracking().SingleAsync();
        Assert.False(sharing.IsDeleted);
        Assert.Equal(companyId, sharing.AccessibleCompanyId);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRawMaterialHandler(db, user)
                .Handle(new DeleteRawMaterialCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnsharedRowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedRowAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRawMaterialHandler(db, user)
                .Handle(new DeleteRawMaterialCommand(foreignId), CancellationToken.None));

        var stored = await db.RawMaterials.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_RowSharedToTheCaller_IsReadOnly()
    {
        var userB = UserB();
        var db = await CreateDbAsync(userB);
        var sharedId = await SeedRowAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: [await SeedCompanyAsync(db, "Company B", CompanyB)]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRawMaterialHandler(db, userB)
                .Handle(new DeleteRawMaterialCommand(sharedId), CancellationToken.None));

        var stored = await db.RawMaterials.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_ViewAllToken_MayDeleteAnotherCompanysRow()
    {
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        var foreignId = await SeedRowAsync(db, companyId: CompanyB);

        await new DeleteRawMaterialHandler(db, viewAll)
            .Handle(new DeleteRawMaterialCommand(foreignId), CancellationToken.None);

        var stored = await db.RawMaterials.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db);

        await new DeleteRawMaterialHandler(db, user)
            .Handle(new DeleteRawMaterialCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRawMaterialHandler(db, user)
                .Handle(new DeleteRawMaterialCommand(id), CancellationToken.None));
    }
}
