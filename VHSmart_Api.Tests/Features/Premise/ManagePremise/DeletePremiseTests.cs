using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class DeletePremiseTests
{
    [Fact]
    public async Task Handle_ExistingPremise_SoftDeletesAndFreesUniqueness()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, email: "doomed@premise.my", storeCode: "SC-01");

        await new DeletePremiseHandler(db, user)
            .Handle(new DeletePremiseCommand(premiseId), CancellationToken.None);

        // Soft delete (CodingRules 7.1): the row survives, flagged - and it leaves both
        // unique lists, so the same e-mail/store code can be reused by a new premise.
        var stored = await db.Premises
            .IgnoreQueryFilters()
            .SingleAsync(row => row.Id == premiseId);
        Assert.True(stored.IsDeleted);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.False(await db.Premises.AnyAsync());
        Assert.False(await db.Premises.AnyAsync(
            row => row.Email == "doomed@premise.my" || row.StoreCode == "SC-01"));
    }

    [Fact]
    public async Task Handle_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var foreignId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, "Foreign premise");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeletePremiseHandler(db, user)
                .Handle(new DeletePremiseCommand(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeletePremiseHandler(db, user)
                .Handle(new DeletePremiseCommand(foreignId), CancellationToken.None));
        Assert.False(await db.Premises.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == foreignId && row.IsDeleted));
    }
}
