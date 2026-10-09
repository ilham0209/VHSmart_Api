using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class CreateAuditCriteriaMasterTests
{
    private static CreateAuditCriteriaMasterHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_CreatesCriteriaMasterForTheJwtCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaMasterCommand(AuditCriteriaMasterKind.Criteria, "Cleanliness"),
            CancellationToken.None);

        var stored = await db.AuditCriteriaMasters.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(AuditCriteriaMasterKind.Criteria, stored.Kind);
        Assert.Equal("Cleanliness", stored.Text);
        Assert.Equal(stored.Id, response.Id);
        Assert.Equal("Cleanliness", response.Text);
    }

    [Fact]
    public async Task Handle_SubCriteriaKind_CreatesSubCriteriaMaster()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaMasterCommand(
                AuditCriteriaMasterKind.SubCriteria, "Chiller logs"),
            CancellationToken.None);

        var stored = await db.AuditCriteriaMasters.SingleAsync();
        Assert.Equal(AuditCriteriaMasterKind.SubCriteria, stored.Kind);
        Assert.Equal(AuditCriteriaMasterKind.SubCriteria, response.Kind);
    }

    [Fact]
    public async Task Handle_DuplicateTextInSameKind_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMasterAsync(db, CompanyA, AuditCriteriaMasterKind.Criteria, "Cleanliness");

        // Database.md UQ (CompanyId, Kind, Text) live rows: 409 with a friendly message
        // instead of letting the index fire (the message is ours - flagged).
        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new CreateAuditCriteriaMasterCommand(
                    AuditCriteriaMasterKind.Criteria, "Cleanliness"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameTextUnderTheOtherKind_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMasterAsync(db, CompanyA, AuditCriteriaMasterKind.Criteria, "Cleanliness");

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaMasterCommand(
                AuditCriteriaMasterKind.SubCriteria, "Cleanliness"),
            CancellationToken.None);

        // The unique pair carries Kind: "Cleanliness" may exist once per kind.
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(2, await db.AuditCriteriaMasters.CountAsync());
    }

    [Fact]
    public async Task Handle_SameTextInAnotherCompany_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMasterAsync(db, CompanyB, AuditCriteriaMasterKind.Criteria, "Cleanliness");

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaMasterCommand(
                AuditCriteriaMasterKind.Criteria, "Cleanliness"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(2, await db.AuditCriteriaMasters.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_SoftDeletedDuplicate_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var existing = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.Criteria, "Cleanliness");
        db.AuditCriteriaMasters.Remove(
            await db.AuditCriteriaMasters.SingleAsync(row => row.Id == existing));
        await db.SaveChangesAsync();

        // The UQ filters live rows only ([IsDeleted] = 0): a soft-deleted value frees the
        // text again (Database.md 11).
        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaMasterCommand(
                AuditCriteriaMasterKind.Criteria, "Cleanliness"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(2, await db.AuditCriteriaMasters.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_MissingText_Fails()
    {
        var validator = new CreateAuditCriteriaMasterValidator();

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaMasterCommand(AuditCriteriaMasterKind.Criteria, string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Text is required.");
    }

    [Fact]
    public async Task Validator_TextLongerThan500_Fails()
    {
        var validator = new CreateAuditCriteriaMasterValidator();

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaMasterCommand(
                AuditCriteriaMasterKind.Criteria, new string('x', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Text must be 500 characters or fewer.");
    }

    [Fact]
    public async Task Validator_UnknownKind_Fails()
    {
        var validator = new CreateAuditCriteriaMasterValidator();

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaMasterCommand((AuditCriteriaMasterKind)99, "Cleanliness"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Kind must be Criteria or SubCriteria.");
    }

    [Fact]
    public async Task Validator_ValidShape_Passes()
    {
        var validator = new CreateAuditCriteriaMasterValidator();

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaMasterCommand(AuditCriteriaMasterKind.SubCriteria, "Chiller logs"));

        Assert.True(result.IsValid);
    }
}
