using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;
using static VHSmart_Api.Tests.Features.Product.ManageMenuConcept.MenuConceptTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class DeleteMenuConceptTests
{
    [Fact]
    public async Task Handle_OwnConcept_IsSoftDeletedAndKeepsItsLinkRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedLinkAsync(db, conceptId, menuId);

        await new DeleteMenuConceptHandler(db)
            .Handle(new DeleteMenuConceptCommand(conceptId), CancellationToken.None);

        var stored = await db.MenuConcepts.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.NotNull(stored.SysDateModified);

        // The child list stays with the concept (PD-04's DeleteMenu stance): the rows are only
        // ever reached through the concept's own table, which now answers 404.
        Assert.Equal(1, await db.MenuConceptMenus.IgnoreQueryFilters().CountAsync());

        var list = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);
        Assert.Empty(list.Data);
    }

    [Fact]
    public async Task Handle_UnknownConcept_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuConceptHandler(db)
                .Handle(new DeleteMenuConceptCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedConcept_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        db.MenuConcepts.Remove(await db.MenuConcepts.SingleAsync());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuConceptHandler(db)
                .Handle(new DeleteMenuConceptCommand(conceptId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConceptOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuConceptHandler(db)
                .Handle(new DeleteMenuConceptCommand(conceptId), CancellationToken.None));
    }
}
