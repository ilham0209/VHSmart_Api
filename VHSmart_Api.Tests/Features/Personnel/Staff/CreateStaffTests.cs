using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class CreateStaffTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Guid> AddPeopleValueAsync(
        TestableVHSmartDbContext db, Guid companyId, string category, string name)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PEOPLE,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<(
        Guid TitleId,
        Guid DepartmentId,
        Guid DesignationId,
        Guid IhcRoleId)> PeopleAsync(TestableVHSmartDbContext db, Guid companyId)
    {
        var title = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr");
        var department = await AddPeopleValueAsync(db, companyId, "Department", "Production");
        var designation = await AddPeopleValueAsync(db, companyId, "Designation", "Halal Executive");
        var ihcRole = await AddPeopleValueAsync(db, companyId, "Internal Halal Committee Role", "Member");
        return (title, department, designation, ihcRole);
    }

    private static async Task<CreateStaffCommand> CommandAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string email = "staff@verify.my") =>
        new(
            email,
            await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr"),
            "Siti Aminah",
            "NRIC",
            "900101011234",
            "EMP-01",
            "Female",
            "Islam",
            await AddPeopleValueAsync(db, companyId, "Department", "Production"),
            await AddPeopleValueAsync(db, companyId, "Designation", "Halal Executive"),
            "0312345678",
            "0123456789",
            false,
            null,
            false,
            null);

    private static CreateStaffHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_StoresRowWithCompanyFromUser()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db, CompanyA);

        var response = await NewHandler(db, user).Handle(command, CancellationToken.None);

        Assert.Equal("Siti Aminah", response.Name);
        Assert.Equal("staff@verify.my", response.Email);

        var stored = await db.Staffs.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT (CodingRules 8.1), never from the body.
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.False(stored.HasTyphoidInjection);
        Assert.False(stored.IsIhcMember);
        Assert.Null(stored.Photo);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_ConditionalFlagsYes_StoresExpiryAndRole()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ihcRole = await AddPeopleValueAsync(db, CompanyA, "Internal Halal Committee Role", "Chairman");
        var baseCommand = await CommandAsync(db, CompanyA);
        var command = baseCommand with
        {
            HasTyphoidInjection = true,
            TyphoidExpiryDate = new DateTime(2027, 6, 30),
            IsIhcMember = true,
            IhcRoleId = ihcRole
        };

        await NewHandler(db, user).Handle(command, CancellationToken.None);

        var stored = await db.Staffs.AsNoTracking().SingleAsync();
        Assert.Equal(new DateTime(2027, 6, 30), stored.TyphoidExpiryDate);
        Assert.Equal(ihcRole, stored.IhcRoleId);
    }

    [Fact]
    public async Task Handle_ConditionalFlagsNo_ClearsStaleValues()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ihcRole = await AddPeopleValueAsync(db, CompanyA, "Internal Halal Committee Role", "Member");
        var baseCommand = await CommandAsync(db, CompanyA);
        // The client sent values although the flags say No: the server stores the consistent pair.
        var command = baseCommand with
        {
            HasTyphoidInjection = false,
            TyphoidExpiryDate = new DateTime(2027, 6, 30),
            IsIhcMember = false,
            IhcRoleId = ihcRole
        };

        await NewHandler(db, user).Handle(command, CancellationToken.None);

        var stored = await db.Staffs.AsNoTracking().SingleAsync();
        Assert.Null(stored.TyphoidExpiryDate);
        Assert.Null(stored.IhcRoleId);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db, CompanyA);
        await NewHandler(db, user).Handle(command, CancellationToken.None);

        var second = await CommandAsync(db, CompanyA, email: "other@verify.my");
        var duplicate = second with { Email = command.Email };
        await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler(db, user).Handle(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameEmailInAnotherCompany_DoesNotConflict()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var companyARow = await NewHandler(db, userA)
            .Handle(await CommandAsync(db, CompanyA, email: "shared@verify.my"), CancellationToken.None);

        var userB = UserB();
        var dbB = TestDbFactory.Create(databaseName, userB);
        var companyBRow = await NewHandler(dbB, userB)
            .Handle(await CommandAsync(dbB, CompanyB, email: "shared@verify.my"), CancellationToken.None);

        Assert.NotEqual(companyARow.Id, companyBRow.Id);
        Assert.Equal(CompanyB, (await dbB.Staffs.AsNoTracking().SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateStaffValidator(db, user);

        var result = await validator.ValidateAsync(new CreateStaffCommand(
            string.Empty, Guid.Empty, string.Empty,
            null, null, null, null, null,
            Guid.Empty, Guid.Empty,
            null, null, false, null, false, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateStaffCommand.Email));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateStaffCommand.Name));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateStaffCommand.TitleId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateStaffCommand.DepartmentId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateStaffCommand.DesignationId));
    }

    [Fact]
    public async Task Validator_TyphoidYesWithoutExpiry_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = (await CommandAsync(db, CompanyA)) with { HasTyphoidInjection = true };

        var result = await new CreateStaffValidator(db, user).ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(CreateStaffCommand.TyphoidExpiryDate));
    }

    [Fact]
    public async Task Validator_IhcMemberYesWithoutRole_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = (await CommandAsync(db, CompanyA)) with { IsIhcMember = true };

        var result = await new CreateStaffValidator(db, user).ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(CreateStaffCommand.IhcRoleId)
                && failure.ErrorMessage == "IHC role is required when IHC member is Yes.");
    }

    [Fact]
    public async Task Validator_UnknownOrForeignPeopleValue_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db, CompanyA);
        var validator = new CreateStaffValidator(db, user);

        var unknown = await validator.ValidateAsync(command with { TitleId = Guid.NewGuid() });
        Assert.False(unknown.IsValid);
        Assert.Contains(
            unknown.Errors,
            failure => failure.ErrorMessage == "Title not found.");

        // The People values are per company (spec 3.3): another tenant's row is "not found".
        var foreignTitle = await AddPeopleValueAsync(db, CompanyB, "Title of Honour", "Dr");
        var foreign = await validator.ValidateAsync(command with { TitleId = foreignTitle });
        Assert.False(foreign.IsValid);
        Assert.Contains(
            foreign.Errors,
            failure => failure.ErrorMessage == "Title not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db, CompanyA);

        var result = await new CreateStaffValidator(db, user).ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
