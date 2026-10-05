using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class BulkDeleteRawMaterialsTests
{
    [Fact]
    public async Task Handle_MultipleRows_SoftDeletesThemAll()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var first = await SeedRowAsync(db, ingredientCode: "RM-001");
        var second = await SeedRowAsync(db, ingredient: "Corn Flour", ingredientCode: "RM-002");

        await new BulkDeleteRawMaterialsHandler(db, user)
            .Handle(new BulkDeleteRawMaterialsCommand([first, second]), CancellationToken.None);

        Assert.Empty(await db.RawMaterials.ToArrayAsync());
        Assert.Equal(2, await db.RawMaterials.IgnoreQueryFilters().CountAsync());
        Assert.All(
            await db.RawMaterials.IgnoreQueryFilters().ToListAsync(),
            row => Assert.True(row.IsDeleted));
    }

    [Fact]
    public async Task Handle_DuplicateIds_AreDeletedOnce()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db);

        await new BulkDeleteRawMaterialsHandler(db, user)
            .Handle(new BulkDeleteRawMaterialsCommand([id, id]), CancellationToken.None);

        Assert.Equal(1, await db.RawMaterials.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundAndDeletesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new BulkDeleteRawMaterialsHandler(db, user)
                .Handle(new BulkDeleteRawMaterialsCommand([id, Guid.NewGuid()]), CancellationToken.None));

        // All-or-nothing: a stale selection never half-applies.
        Assert.False((await db.RawMaterials.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndDeletesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var own = await SeedRowAsync(db, ingredientCode: "RM-001");
        var foreign = await SeedRowAsync(db, ingredient: "Other", ingredientCode: "RM-002", companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new BulkDeleteRawMaterialsHandler(db, user)
                .Handle(new BulkDeleteRawMaterialsCommand([own, foreign]), CancellationToken.None));

        Assert.Equal(0, await db.RawMaterials.IgnoreQueryFilters().CountAsync(row => row.IsDeleted));
    }

    [Fact]
    public async Task Validator_MissingIds_Fails()
    {
        var validator = new BulkDeleteRawMaterialsValidator();

        var missing = await validator.ValidateAsync(new BulkDeleteRawMaterialsCommand(null));
        var empty = await validator.ValidateAsync(new BulkDeleteRawMaterialsCommand([]));

        Assert.False(missing.IsValid);
        Assert.Contains(
            missing.Errors,
            failure => failure.ErrorMessage == "No rows selected.");
        Assert.False(empty.IsValid);
        Assert.Contains(
            empty.Errors,
            failure => failure.ErrorMessage == "No rows selected.");
    }
}
