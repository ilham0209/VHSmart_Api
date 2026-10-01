using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class UpdateUserTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesFieldsAndReplacesCompanies()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "old@example.com", companyA);

        var response = await new UpdateUserHandler(db, caller).Handle(
            new UpdateUserCommand(
                user.Id, "  siti aminah ", "Siti.Aminah@Example.COM",
                RoleSeedData.AuditorRoleId, "012-9998888", IsActive: false, [companyB]),
            default);

        Assert.Equal(user.Id, response.Id);
        Assert.Equal("SITI AMINAH", response.Name);
        Assert.Equal("siti.aminah@example.com", response.Email);
        Assert.Equal(RoleSeedData.AuditorRoleId, response.RoleId);
        Assert.False(response.IsActive);

        var stored = await db.Users.AsNoTracking().SingleAsync(row => row.Id == user.Id);
        Assert.Equal("SITI AMINAH", stored.Name);
        Assert.False(stored.IsActive);

        var memberships = await db.UserCompanies.AsNoTracking()
            .Where(row => row.UserId == user.Id)
            .ToListAsync();
        var single = Assert.Single(memberships);
        Assert.Equal(companyB, single.CompanyId);
        // The default link was removed with company A, so the survivor is promoted.
        Assert.True(single.IsDefault);
    }

    [Fact]
    public async Task Handle_UnchangedEmail_DoesNotConflictWithItself()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "same@example.com", company);

        var response = await new UpdateUserHandler(db, caller).Handle(
            Command(user.Id, email: "same@example.com", companyIds: [company]),
            default);

        Assert.Equal("same@example.com", response.Email);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var first = await UsersTestData.AddUserAsync(db, "first@example.com", company);
        _ = await UsersTestData.AddUserAsync(db, "second@example.com", company);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateUserHandler(db, caller).Handle(
                Command(first.Id, email: "SECOND@example.com", companyIds: [company]),
                default));
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateUserHandler(db, caller).Handle(
                Command(Guid.NewGuid(), companyIds: [company]),
                default));
    }

    [Fact]
    public async Task Handle_UserOfAnotherCompany_ThrowsNotFoundForCompanyAdmin()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var seedCaller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, seedCaller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var userB = await UsersTestData.AddUserAsync(db, "b-user@example.com", companyB);

        var companyAdmin = UsersTestData.CompanyAdmin(companyA);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateUserHandler(db, companyAdmin).Handle(
                Command(userB.Id, companyIds: [companyB]),
                default));
    }

    [Fact]
    public async Task Handle_CompanyAdmin_CannotStripCompanyOutsideTheirScope()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var seedCaller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, seedCaller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "multi@example.com", companyA, companyB);

        var companyAdmin = UsersTestData.CompanyAdmin(companyA);
        await new UpdateUserHandler(db, companyAdmin).Handle(
            Command(user.Id, companyIds: [companyA]),
            default);

        // The B link is outside company A's scope: it must survive untouched.
        var memberships = await db.UserCompanies.AsNoTracking()
            .Where(row => row.UserId == user.Id)
            .OrderBy(row => row.CompanyId)
            .ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, row => row.CompanyId == companyA);
        Assert.Contains(memberships, row => row.CompanyId == companyB);
    }

    [Fact]
    public async Task Handle_CompanyAdmin_CannotAssignAnotherCompany()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var seedCaller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, seedCaller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "a-user@example.com", companyA);

        var companyAdmin = UsersTestData.CompanyAdmin(companyA);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateUserHandler(db, companyAdmin).Handle(
                Command(user.Id, companyIds: [companyA, companyB]),
                default));
    }

    [Fact]
    public async Task Handle_RemovedDefault_PromotesRemainingCompany()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "multi@example.com", companyA, companyB);

        await new UpdateUserHandler(db, caller).Handle(
            Command(user.Id, companyIds: [companyB]),
            default);

        var single = await db.UserCompanies.AsNoTracking()
            .SingleAsync(row => row.UserId == user.Id);
        Assert.Equal(companyB, single.CompanyId);
        Assert.True(single.IsDefault);
    }

    [Fact]
    public async Task Handle_ReaddingPreviouslyRemovedCompany_CreatesNoDuplicateRow()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "multi@example.com", companyA, companyB);

        var companyAdmin = caller;
        await new UpdateUserHandler(db, companyAdmin).Handle(
            Command(user.Id, companyIds: [companyA]), default);
        await new UpdateUserHandler(db, companyAdmin).Handle(
            Command(user.Id, companyIds: [companyA, companyB]), default);

        var memberships = await db.UserCompanies.AsNoTracking()
            .Where(row => row.UserId == user.Id)
            .ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.Single(memberships, row => row.CompanyId == companyA && row.IsDefault);
        Assert.Single(memberships, row => row.CompanyId == companyB);
    }

    [Fact]
    public void Validator_EmptyCompanyList_FailsWithSpecMessage()
    {
        var validator = new UpdateUserValidator();

        var result = validator.Validate(Command(Guid.NewGuid(), companyIds: []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "CompanyIds"
                && error.ErrorMessage == "Please add List of Company");
    }

    [Fact]
    public void Validator_MissingRequiredFields_Fails()
    {
        var validator = new UpdateUserValidator();

        var result = validator.Validate(new UpdateUserCommand(
            Guid.Empty, string.Empty, string.Empty, Guid.Empty, null, true, []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Id");
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "RoleId");
        Assert.Contains(result.Errors, error => error.PropertyName == "CompanyIds");
    }

    [Fact]
    public void Validator_MalformedEmail_Fails()
    {
        var validator = new UpdateUserValidator();

        var result = validator.Validate(Command(Guid.NewGuid(), email: "not-an-email"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
    }

    [Fact]
    public void Validator_NameLongerThanColumn_Fails()
    {
        var validator = new UpdateUserValidator();

        var result = validator.Validate(
            Command(Guid.NewGuid(), name: new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact]
    public void Validator_FullValidCommand_Passes()
    {
        var validator = new UpdateUserValidator();

        var result = validator.Validate(
            Command(Guid.NewGuid(), "SITI AMINAH", "siti@example.com"));

        Assert.True(result.IsValid);
    }

    private static UpdateUserCommand Command(
        Guid id,
        string name = "SITI AMINAH",
        string email = "siti@example.com",
        IReadOnlyList<Guid>? companyIds = null) =>
        new(
            id, name, email, RoleSeedData.AuditorRoleId, "0123456789", true,
            companyIds ?? [Guid.NewGuid()]);
}
