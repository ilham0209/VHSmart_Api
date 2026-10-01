using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Features.Auth;
using VHSmart_Api.Features.Auth.Activation;
using VHSmart_Api.Features.Auth.Login;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class UserApiTests
{
    private const string UsersRoute = "/api/admin/users";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    [Fact]
    public async Task GetUsers_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(UsersRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithoutAdminUsersPermission_ReturnsForbidden()
    {
        using var factory = new UsersApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(UsersRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_PlatformAdmin_ReturnsAllUsers()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var companyA = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var companyB = await SeedCompanyAsync(factory, "BETA SDN BHD");
        _ = await SeedUserAsync(factory, "a-user@example.com", companyA);
        _ = await SeedUserAsync(factory, "b-user@example.com", companyB);
        using var client = CreateClient(factory);

        var response = await client.GetAsync(UsersRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllUsersResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(2, grid.TotalRecords);
        Assert.Contains(grid.Data, row => row.Email == "a-user@example.com");
        Assert.Contains(grid.Data, row => row.Email == "b-user@example.com");
        Assert.All(grid.Data, row => Assert.Equal("Active", row.Status));
    }

    [Fact]
    public async Task GetUsers_CompanyAdmin_SeesOnlyOwnCompanyUsers()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var companyA = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var companyB = await SeedCompanyAsync(factory, "BETA SDN BHD");
        var userA = await SeedUserAsync(factory, "a-user@example.com", companyA);
        _ = await SeedUserAsync(factory, "b-user@example.com", companyB);
        using var client = CreateClient(factory, userA.Id, companyA, isPlatformAdmin: false);

        var response = await client.GetAsync(UsersRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllUsersResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var single = Assert.Single(grid.Data);
        Assert.Equal(userA.Id, single.Id);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsActivationTokenAndStoresPendingUser()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        using var client = CreateClient(factory);

        var command = new CreateUserCommand(
            "  ahmad razak ", "Ahmad.Razak@Example.COM", AdminRoleId, "0123456789", [company]);

        var response = await client.PostAsJsonAsync(UsersRoute, command);
        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("AHMAD RAZAK", created.Name);
        Assert.Equal("ahmad.razak@example.com", created.Email);
        Assert.False(string.IsNullOrWhiteSpace(created.ActivationToken));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.Users.SingleAsync(row => row.Id == created.Id);
        Assert.False(stored.IsActive);
        Assert.Equal(string.Empty, stored.PasswordHash);
        var membership = await db.UserCompanies.SingleAsync(row => row.UserId == stored.Id);
        Assert.Equal(company, membership.CompanyId);
        Assert.True(membership.IsDefault);
        var tokenRow = await db.UserTokens.SingleAsync(row => row.UserId == stored.Id);
        Assert.Equal(OneTimeToken.Hash(created.ActivationToken), tokenRow.TokenHash);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new UsersApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var command = new CreateUserCommand(
            "AHMAD", "ahmad@example.com", AdminRoleId, null, [Guid.NewGuid()]);

        var response = await client.PostAsJsonAsync(UsersRoute, command);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        _ = await SeedUserAsync(factory, "taken@example.com", company);
        using var client = CreateClient(factory);

        var command = new CreateUserCommand("AHMAD", "taken@example.com", AdminRoleId, null, [company]);

        var response = await client.PostAsJsonAsync(UsersRoute, command);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCompanies_ReturnsBadRequestWithSpecMessage()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var command = new CreateUserCommand("AHMAD", "ahmad@example.com", AdminRoleId, null, []);

        var response = await client.PostAsJsonAsync(UsersRoute, command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors.Values.SelectMany(messages => messages),
            message => message == "Please add List of Company");
    }

    [Fact]
    public async Task Create_UnknownCompany_ReturnsNotFound()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var command = new CreateUserCommand(
            "AHMAD", "ahmad@example.com", AdminRoleId, null, [Guid.NewGuid()]);

        var response = await client.PostAsJsonAsync(UsersRoute, command);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOptions_ReturnsRolesAndCompanies()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        using var client = CreateClient(factory);

        // The literal route must win over {id:guid} - never bind "options" as a user id.
        var response = await client.GetAsync($"{UsersRoute}/options");
        var options = await response.Content.ReadFromJsonAsync<UserOptionsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal(3, options.Roles.Count);
        var single = Assert.Single(options.Companies);
        Assert.Equal(company, single.Id);
    }

    [Fact]
    public async Task GetById_UnknownUser_ReturnsNotFound()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var response = await client.GetAsync($"{UsersRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UserOfAnotherCompany_ReturnsNotFoundForCompanyAdmin()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var companyA = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var companyB = await SeedCompanyAsync(factory, "BETA SDN BHD");
        var userB = await SeedUserAsync(factory, "b-user@example.com", companyB);
        using var client = CreateClient(factory, Guid.NewGuid(), companyA, isPlatformAdmin: false);

        var response = await client.GetAsync($"{UsersRoute}/{userB.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidCommand_StoresChanges()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var user = await SeedUserAsync(factory, "old@example.com", company);
        using var client = CreateClient(factory);

        var command = new UpdateUserCommand(
            user.Id, "  siti aminah ", "siti@example.com", AdminRoleId, "0123456789",
            IsActive: false, [company]);

        var response = await client.PutAsJsonAsync($"{UsersRoute}/{user.Id}", command);
        var updated = await response.Content.ReadFromJsonAsync<UserDetailsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("SITI AMINAH", updated.Name);
        Assert.False(updated.IsActive);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.Users.SingleAsync(row => row.Id == user.Id);
        Assert.Equal("SITI AMINAH", stored.Name);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new UsersApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var command = new UpdateUserCommand(
            Guid.NewGuid(), "SITI", "siti@example.com", AdminRoleId, null, true, [Guid.NewGuid()]);

        var response = await client.PutAsJsonAsync($"{UsersRoute}/{Guid.NewGuid()}", command);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingUser_ReturnsNoContentAndHidesRow()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var user = await SeedUserAsync(factory, "leaver@example.com", company);
        using var client = CreateClient(factory);

        var response = await client.DeleteAsync($"{UsersRoute}/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var followUp = await client.GetAsync($"{UsersRoute}/{user.Id}");
        Assert.Equal(HttpStatusCode.NotFound, followUp.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new UsersApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateClient(factory);

        var response = await client.DeleteAsync($"{UsersRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateActivationToken_PendingUser_ReturnsFreshToken()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var user = await SeedUserAsync(factory, "pending@example.com", company, isActive: false);
        using var client = CreateClient(factory);

        var response = await client.PostAsync($"{UsersRoute}/{user.Id}/activation-token", null);
        var payload = await response.Content.ReadFromJsonAsync<CreateActivationTokenResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(user.Id, payload.UserId);
        Assert.False(string.IsNullOrWhiteSpace(payload.ActivationToken));
    }

    [Fact]
    public async Task CreateActivationToken_AlreadyActivated_ReturnsUnprocessableEntity()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        var user = await SeedUserAsync(factory, "done@example.com", company);
        using var client = CreateClient(factory);

        var response = await client.PostAsync($"{UsersRoute}/{user.Id}/activation-token", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Activate_ThenLogin_FullFlowWorksEndToEnd()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var company = await SeedCompanyAsync(factory, "ALPHA SDN BHD");
        using var adminClient = CreateClient(factory);

        var created = await (await adminClient.PostAsJsonAsync(
            UsersRoute,
            new CreateUserCommand(
                "NEW USER", "new-user@example.com", AdminRoleId, null, [company])))
            .Content.ReadFromJsonAsync<CreateUserResponse>();
        Assert.NotNull(created);

        // Activation is anonymous (CodingRules 8.2) and returns the default password once.
        using var anonymous = factory.CreateClient();
        var activateResponse = await anonymous.PostAsJsonAsync(
            "/api/auth/activate",
            new ActivateCommand(created.ActivationToken));
        var activation = await activateResponse.Content.ReadFromJsonAsync<ActivateResponse>();

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.NotNull(activation);
        Assert.Equal("new-user@example.com", activation.Email);

        var loginResponse = await anonymous.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand("new-user@example.com", activation.Password));
        var session = await loginResponse.Content.ReadFromJsonAsync<SessionResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(session);
        Assert.Equal(created.Id, session.UserId);
        // The system-generated default must be changed at the first login (spec 3.2).
        Assert.True(session.MustChangePassword);
    }

    [Fact]
    public async Task Activate_UnknownToken_ReturnsNotFound()
    {
        using var factory = new UsersApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/activate",
            new ActivateCommand("NOT-A-REAL-TOKEN"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateClient(
        UsersApiFactory factory,
        Guid? userId = null,
        Guid? companyId = null,
        bool isPlatformAdmin = true)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateToken(
                userId ?? Guid.NewGuid(),
                companyId ?? Guid.NewGuid(),
                isPlatformAdmin));
        return client;
    }

    private static async Task<Guid> SeedCompanyAsync(UsersApiFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        return await UsersTestData.AddCompanyAsync(db, name);
    }

    private static async Task<UserEntity> SeedUserAsync(
        UsersApiFactory factory,
        string email,
        Guid companyId,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var user = await UsersTestData.AddUserAsync(db, email, companyId);
        if (!isActive)
        {
            user.IsActive = false;
            user.ActivatedAt = null;
            await db.SaveChangesAsync();
        }

        return user;
    }
}
