using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VHSmart_Api.Features.Account.Password;
using VHSmart_Api.Features.Account.Profile;
using VHSmart_Api.Features.Account.ProfilePicture;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Tests.Features.Auth;

namespace VHSmart_Api.Tests.Features.Account;

public class AccountApiTests
{
    private const string Password = "Passw0rd!";

    private const string NewPassword = "N3wPassw0rd!";

    private const string ProfileRoute = "/api/account/profile";

    private const string PasswordRoute = "/api/account/password";

    private const string PictureRoute = "/api/account/profile-picture";

    [Fact]
    public async Task Profile_WithoutToken_Returns401()
    {
        using var factory = new AuthApiFactory();
        await factory.SeedUserAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithToken_ReturnsNameEmailAndRole()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<GetProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("API TEST USER", profile.Name);
        Assert.Equal("admin@example.com", profile.Email);
        Assert.Equal("VH Smart Admin", profile.RoleName);
    }

    [Fact]
    public async Task Profile_WithoutAccountSettingPermission_Returns403()
    {
        using var factory = new AuthApiFactory();
        var role = await factory.SeedRoleWithoutPermissionsAsync();
        var user = await factory.SeedUserAsync(roleId: role.Id, isPlatformAdmin: false);
        using var client = factory.CreateClient();
        Authorize(client, factory, user);

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithToken_Returns200()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);

        var response = await client.PutAsJsonAsync(
            PasswordRoute, new ChangePasswordCommand(Password, NewPassword, NewPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task ChangePassword_WrongOldPassword_Returns422()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);

        var response = await client.PutAsJsonAsync(
            PasswordRoute, new ChangePasswordCommand("wrong-old", NewPassword, NewPassword));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("Old password is incorrect.", problem!.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ChangePassword_MissingNewPassword_Returns400WithFieldErrors()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);

        var response = await client.PutAsJsonAsync(
            PasswordRoute, new ChangePasswordCommand(Password, string.Empty, string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var errors = problem!.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("NewPassword", out _));
        Assert.True(errors.TryGetProperty("ConfirmPassword", out _));
    }

    [Fact]
    public async Task UploadProfilePicture_WithImage_Returns200AndPictureIsServed()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);
        var fileContent = new ByteArrayContent([137, 80, 78, 71]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var form = new MultipartFormDataContent
        {
            { fileContent, "file", "avatar.png" }
        };

        var upload = await client.PostAsync(PictureRoute, form);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var picture = await upload.Content.ReadFromJsonAsync<UploadProfilePictureResponse>();
        Assert.Equal("avatar.png", picture!.FileName);

        var download = await client.GetAsync(PictureRoute);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UploadProfilePicture_WithoutFile_Returns400()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);
        // A part named "file" without a filename does not bind to IFormFile: the validator
        // has to reject it with a field error (an entirely empty multipart body never reaches
        // the validator - MVC already fails reading the form).
        using var form = new MultipartFormDataContent
        {
            { new StringContent(string.Empty), "file" }
        };

        var response = await client.PostAsync(PictureRoute, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(body.Contains("\"File\"", StringComparison.Ordinal), body);
    }

    [Fact]
    public async Task GetProfilePicture_WithoutPicture_Returns404()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user);

        var response = await client.GetAsync(PictureRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void Authorize(
        HttpClient client,
        AuthApiFactory factory,
        UserEntity user)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateTokenFor(user, Guid.NewGuid()));
    }
}
