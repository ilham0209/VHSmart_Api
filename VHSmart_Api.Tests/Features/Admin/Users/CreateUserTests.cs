using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class CreateUserTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresUpperNameAndFirstCompanyDefault()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");

        var response = await new CreateUserHandler(db, caller, UsersTestData.Config())
            .Handle(Command("  ahmad razak ", "Ahmad.Razak@Example.COM", [companyA, companyB]), default);

        Assert.Equal("AHMAD RAZAK", response.Name);
        Assert.Equal("ahmad.razak@example.com", response.Email);
        Assert.False(string.IsNullOrWhiteSpace(response.ActivationToken));

        var user = await db.Users.AsNoTracking().SingleAsync(row => row.Id == response.Id);
        Assert.Equal("AHMAD RAZAK", user.Name);
        Assert.Equal(string.Empty, user.PasswordHash);
        Assert.False(user.IsActive);
        Assert.Null(user.ActivatedAt);
        Assert.False(user.IsPlatformAdmin);

        var memberships = await db.UserCompanies.AsNoTracking()
            .Where(row => row.UserId == user.Id)
            .OrderBy(row => row.IsDefault ? 0 : 1)
            .ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.True(memberships[0].IsDefault);
        Assert.Equal(companyA, memberships[0].CompanyId);
        Assert.False(memberships[1].IsDefault);
        Assert.Equal(companyB, memberships[1].CompanyId);
    }

    [Fact]
    public async Task Handle_IssuesActivationTokenStoredAsHashOnly()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");

        var response = await new CreateUserHandler(db, caller, UsersTestData.Config())
            .Handle(Command("AHMAD", "ahmad@example.com", [company]), default);

        var token = await db.UserTokens.AsNoTracking().SingleAsync();
        Assert.Equal(UserTokenPurpose.Activation, token.Purpose);
        Assert.Equal(response.Id, token.UserId);
        Assert.Null(token.UsedAt);
        Assert.True(token.ExpiresAt > DateTime.UtcNow.AddHours(47));
        // The raw token is returned once; the row keeps only its hash (Database.md 5).
        Assert.DoesNotContain(token.TokenHash, response.ActivationToken);
        Assert.Equal(OneTimeToken.Hash(response.ActivationToken), token.TokenHash);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        await UsersTestData.AddUserAsync(db, "taken@example.com", company);

        // Case-insensitive: the stored address is lower-cased, the request may not be.
        await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateUserHandler(db, caller, UsersTestData.Config())
                .Handle(Command("AHMAD", "TAKEN@example.com", [company]), default));
    }

    [Fact]
    public async Task Handle_SoftDeletedEmail_IsFreeAgain()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var old = await UsersTestData.AddUserAsync(db, "taken@example.com", company);
        old.IsDeleted = true;
        await db.SaveChangesAsync();

        var response = await new CreateUserHandler(db, caller, UsersTestData.Config())
            .Handle(Command("AHMAD", "taken@example.com", [company]), default);

        // Spec 21.9: uniqueness is only checked against live users, so the create went through.
        Assert.NotEqual(old.Id, response.Id);
        Assert.Equal("taken@example.com", response.Email);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateUserHandler(db, caller, UsersTestData.Config())
                .Handle(Command("AHMAD", "ahmad@example.com", [Guid.NewGuid()]), default));
    }

    [Fact]
    public async Task Handle_CompanyAdminAssigningOtherCompany_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var companyA = Guid.NewGuid();
        var caller = UsersTestData.CompanyAdmin(companyA);
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var ownCompany = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var otherCompany = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        caller = UsersTestData.CompanyAdmin(ownCompany);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateUserHandler(db, caller, UsersTestData.Config())
                .Handle(Command("AHMAD", "ahmad@example.com", [otherCompany]), default));
    }

    [Fact]
    public async Task Handle_UnknownRole_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateUserHandler(db, caller, UsersTestData.Config())
                .Handle(
                    Command("AHMAD", "ahmad@example.com", [company], roleId: Guid.NewGuid()),
                    default));
    }

    [Fact]
    public async Task Handle_NonPlatformAdminAssigningCompanyRole_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var ownCompany = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");

        // A per-company role exists but may only be assigned by a platform admin.
        var companyRole = new RoleEntity { Name = "COMPANY ROLE", CompanyId = ownCompany };
        db.Roles.Add(companyRole);
        await db.SaveChangesAsync();

        var companyAdmin = UsersTestData.CompanyAdmin(ownCompany);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateUserHandler(db, companyAdmin, UsersTestData.Config())
                .Handle(
                    Command("AHMAD", "ahmad@example.com", [ownCompany], roleId: companyRole.Id),
                    default));
    }

    [Fact]
    public async Task Handle_EmptyCompanyList_ThrowsBusinessRuleWithSpecMessage()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CreateUserHandler(db, caller, UsersTestData.Config())
                .Handle(Command("AHMAD", "ahmad@example.com", []), default));

        Assert.Equal("Please add List of Company", exception.Message);
    }

    [Fact]
    public void Validator_EmptyCompanyList_FailsWithSpecMessage()
    {
        var validator = new CreateUserValidator();

        var result = validator.Validate(Command("AHMAD", "ahmad@example.com", []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "CompanyIds"
                && error.ErrorMessage == "Please add List of Company");
    }

    [Fact]
    public void Validator_MissingRequiredFields_Fails()
    {
        var validator = new CreateUserValidator();

        var result = validator.Validate(new CreateUserCommand(
            string.Empty, string.Empty, Guid.Empty, null, []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "RoleId");
        Assert.Contains(result.Errors, error => error.PropertyName == "CompanyIds");
    }

    [Fact]
    public void Validator_MalformedEmail_Fails()
    {
        var validator = new CreateUserValidator();

        var result = validator.Validate(Command("AHMAD", "not-an-email", [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
    }

    [Fact]
    public void Validator_NameLongerThanColumn_Fails()
    {
        var validator = new CreateUserValidator();

        var result = validator.Validate(
            Command(new string('x', 201), "ahmad@example.com", [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact]
    public void Validator_ContactNoLongerThanColumn_Fails()
    {
        var validator = new CreateUserValidator();

        var result = validator.Validate(
            Command("AHMAD", "ahmad@example.com", [Guid.NewGuid()], contactNo: new string('1', 31)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ContactNo");
    }

    [Fact]
    public void Validator_FullValidCommand_Passes()
    {
        var validator = new CreateUserValidator();

        var result = validator.Validate(
            Command("AHMAD RAZAK", "ahmad@example.com", [Guid.NewGuid()], contactNo: "0123456789"));

        Assert.True(result.IsValid);
    }

    private static CreateUserCommand Command(
        string name,
        string email,
        IReadOnlyList<Guid> companyIds,
        string? contactNo = null,
        Guid? roleId = null) =>
        new(name, email, roleId ?? RoleSeedData.VhSmartAdminRoleId, contactNo, companyIds);
}
