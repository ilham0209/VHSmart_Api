using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class UpdatePremiseTagTests
{
    [Fact]
    public async Task Handle_ExistingTag_SetsTagIdWithAuditStamp()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var tagId = await PremiseTestData.SeedPremiseTagAsync(db, user.CompanyId);

        await new UpdatePremiseTagHandler(db, user)
            .Handle(new UpdatePremiseTagCommand(premiseId, tagId), CancellationToken.None);

        var stored = await db.Premises.AsNoTracking().SingleAsync();
        Assert.Equal(tagId, stored.TagId);
        Assert.Equal(user.UserId, stored.SysUserModified);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var tagId = await PremiseTestData.SeedPremiseTagAsync(db, user.CompanyId);
        var foreignId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, "Foreign premise");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdatePremiseTagHandler(db, user)
                .Handle(new UpdatePremiseTagCommand(Guid.NewGuid(), tagId),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdatePremiseTagHandler(db, user)
                .Handle(new UpdatePremiseTagCommand(foreignId, tagId),
                    CancellationToken.None));
        Assert.False(await db.Premises.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == foreignId && row.TagId == tagId));
    }

    [Fact]
    public async Task Validator_NullTagId_FailsWithRequiredMessage()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var validator = new UpdatePremiseTagValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdatePremiseTagCommand(premiseId, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Premise tag is required.");
        // Cascade stop: the "not found" rule must not pile a second message on.
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task Validator_UnknownOrForeignOrWrongCategoryTag_Fails()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var foreignTagId = await PremiseTestData.SeedPremiseTagAsync(
            db, foreignUser.CompanyId, "Foreign tag");
        var wrongCategoryTagId = await PremiseTestData.SeedBrandAsync(
            db, user.CompanyId, "Seri Rasa");
        var validator = new UpdatePremiseTagValidator(db, user);

        var unknown = await validator.ValidateAsync(
            new UpdatePremiseTagCommand(premiseId, Guid.NewGuid()));
        var foreign = await validator.ValidateAsync(
            new UpdatePremiseTagCommand(premiseId, foreignTagId));
        var wrongCategory = await validator.ValidateAsync(
            new UpdatePremiseTagCommand(premiseId, wrongCategoryTagId));
        var emptyId = await validator.ValidateAsync(
            new UpdatePremiseTagCommand(Guid.Empty, Guid.NewGuid()));

        Assert.False(unknown.IsValid);
        Assert.Contains(unknown.Errors,
            failure => failure.ErrorMessage == "Premise tag not found.");
        Assert.False(foreign.IsValid);
        Assert.Contains(foreign.Errors,
            failure => failure.ErrorMessage == "Premise tag not found.");
        Assert.False(wrongCategory.IsValid);
        Assert.Contains(wrongCategory.Errors,
            failure => failure.ErrorMessage == "Premise tag not found.");
        Assert.False(emptyId.IsValid);
    }

    [Fact]
    public async Task Validator_OwnPremiseTag_Passes()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var tagId = await PremiseTestData.SeedPremiseTagAsync(db, user.CompanyId);
        var validator = new UpdatePremiseTagValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdatePremiseTagCommand(premiseId, tagId));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
