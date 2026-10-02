using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

public class PremiseApiTests
{
    private const string Route = "/api/premise/manage-premise";

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(PremiseApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    // The "Premise Information" form as JSON (spec 7.7): For Company* is read-only display
    // and comes from the JWT, so it is never part of the body. contactStaffIds/hostels ride
    // along for the PUT (create ignores unknown properties).
    private static object PremiseBody(
        Guid countryId,
        string premiseType = "Factory",
        string name = "Seri Rasa Factory",
        string? email = null,
        string? storeCode = null,
        Guid? premiseManagerStaffId = null,
        Guid? prayerRoomAvailabilityId = null,
        IReadOnlyList<Guid>? contactStaffIds = null,
        IReadOnlyList<object>? hostels = null) => new
        {
            premiseType,
            name,
            email = email ?? $"{Guid.NewGuid():N}@premise.my",
            storeCode,
            premiseManagerStaffId,
            premiseManagerName = (string?)null,
            areaManagerStaffId = (Guid?)null,
            operationManagerStaffId = (Guid?)null,
            businessRegistrationNo = "BRN-001",
            googleMapLink = (string?)null,
            address1 = "1 Jalan Verify",
            address2 = "Taman Industri",
            address3 = (string?)null,
            postcode = "40000",
            city = "Shah Alam",
            district = "Selangor",
            countryId,
            state = "Selangor",
            telephone = "0312345678",
            fax = (string?)null,
            openingDate = (string?)null,
            closingDate = (string?)null,
            status = "ACTIVE",
            prayerRoomAvailabilityId,
            contactStaffIds = contactStaffIds ?? [],
            hostels = hostels ?? []
        };

    private static object HostelBody(string hostelName, Guid? id = null) => new
    {
        id,
        hostelName,
        address = (string?)null,
        tenancyExpiryDate = (string?)null,
        contactPerson = (string?)null,
        phoneNo = (string?)null
    };

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new PremiseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new PremiseApiFactory(grantView: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new PremiseApiFactory(grantCreate: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, PremiseBody(await factory.MalaysiaCountryIdAsync()), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new PremiseApiFactory(grantEdit: false);
        await factory.SeedDatabaseAsync();
        var premiseId = await factory.SeedPremiseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{premiseId}", PremiseBody(await factory.MalaysiaCountryIdAsync()), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new PremiseApiFactory(grantDelete: false);
        await factory.SeedDatabaseAsync();
        var premiseId = await factory.SeedPremiseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{premiseId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ShowsOwnPremisesWithJoinsAndFilter()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var managerId = await factory.SeedStaffAsync("Ahmad Razali");
        await factory.SeedPremiseAsync(name: "Zeta Depot", storeCode: "SC-90");
        await factory.SeedPremiseAsync(
            name: "Rosa Cafe",
            premiseType: PremiseType.RestaurantsAndCafe,
            areaManagerStaffId: managerId);
        using var client = CreateAuthorizedClient(factory);

        var all = await client.GetAsync(Route);
        var grid = await all.Content
            .ReadFromJsonAsync<DataGridResponse<GetAllPremisesResponse>>(Json);
        var filtered = await client.GetAsync($"{Route}?PremiseType=RestaurantsAndCafe");
        var cafeGrid = await filtered.Content
            .ReadFromJsonAsync<DataGridResponse<GetAllPremisesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.NotNull(grid);
        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Rosa Cafe", "Zeta Depot" }, rows.Select(row => row.StoreName));
        Assert.Equal("1 Jalan Verify Taman Industri", rows[0].Address);
        var cafe = rows.Single(row => row.StoreName == "Rosa Cafe");
        Assert.Equal(PremiseType.RestaurantsAndCafe, cafe.PremiseType);
        Assert.Equal("Ahmad Razali", cafe.AreaManager);
        Assert.NotNull(cafeGrid);
        Assert.Equal("Rosa Cafe", Assert.Single(cafeGrid.Data).StoreName);
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsTheSavedState()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var countryId = await factory.MalaysiaCountryIdAsync();
        var managerId = await factory.SeedStaffAsync("Pengurus Utama");
        var contactId = await factory.SeedStaffAsync("Nur Aisyah");
        var prayerRoomId = await factory.SeedPrayerRoomAsync();
        using var client = CreateAuthorizedClient(factory);

        var createResponse = await client.PostAsJsonAsync(
            Route,
            PremiseBody(
                countryId,
                name: "Seri Rasa Factory",
                storeCode: "SC-01",
                premiseManagerStaffId: managerId,
                prayerRoomAvailabilityId: prayerRoomId),
            Json);
        var created = await createResponse.Content
            .ReadFromJsonAsync<PremiseDetailResponse>(Json);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Seri Rasa Factory", created.Name);
        Assert.Equal("Verify Halal Sdn Bhd", created.Company);
        Assert.Equal("Pengurus Utama", created.PremiseManager);
        Assert.Equal("Available", created.PrayerRoomAvailability);
        Assert.Empty(created.Contacts);

        // The children arrive through Save: PUT with one contact and one hostel.
        var putResponse = await client.PutAsJsonAsync(
            $"{Route}/{created.Id}",
            PremiseBody(
                countryId,
                name: "Seri Rasa Factory",
                storeCode: "SC-01",
                premiseManagerStaffId: managerId,
                prayerRoomAvailabilityId: prayerRoomId,
                contactStaffIds: [contactId],
                hostels: [HostelBody("Hostel A")]),
            Json);
        var getResponse = await client.GetAsync($"{Route}/{created.Id}");
        var loaded = await getResponse.Content
            .ReadFromJsonAsync<PremiseDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        var contact = Assert.Single(loaded.Contacts);
        Assert.Equal("Nur Aisyah", contact.Name);
        Assert.Equal("Halal Executive", contact.Designation);
        Assert.Equal("Hostel A", Assert.Single(loaded.Hostels).HostelName);
    }

    [Fact]
    public async Task Create_MissingRequiredField_ReturnsBadRequest()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        var countryId = await factory.MalaysiaCountryIdAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, PremiseBody(countryId, name: " "), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        var countryId = await factory.MalaysiaCountryIdAsync();
        const string email = "shared@premise.my";
        await factory.SeedPremiseAsync(email: email);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, PremiseBody(countryId, email: email), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownOrForeignPremise_ReturnsNotFound()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        var foreignCompany = await factory.SeedForeignCompanyAsync();
        var foreignId = await factory.SeedPremiseAsync(
            "Foreign premise", companyId: foreignCompany);
        using var client = CreateAuthorizedClient(factory);

        var unknown = await client.GetAsync($"{Route}/{Guid.NewGuid()}");
        var foreign = await client.GetAsync($"{Route}/{foreignId}");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Update_ForeignPremise_ReturnsNotFoundAndChangesNothing()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        var foreignCompany = await factory.SeedForeignCompanyAsync();
        var foreignId = await factory.SeedPremiseAsync(
            "Foreign premise", companyId: foreignCompany);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{foreignId}",
            PremiseBody(await factory.MalaysiaCountryIdAsync(), name: "Renamed"),
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Foreign premise", await factory.PremiseNameAsync(foreignId));
    }

    [Fact]
    public async Task Update_UnknownHostelId_ReturnsNotFound()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        var countryId = await factory.MalaysiaCountryIdAsync();
        var premiseId = await factory.SeedPremiseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{premiseId}",
            PremiseBody(
                countryId,
                hostels: [HostelBody("Ghost Hostel", Guid.NewGuid())]),
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGetById_ReturnsNotFound()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        var premiseId = await factory.SeedPremiseAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{premiseId}");
        var getResponse = await client.GetAsync($"{Route}/{premiseId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Null(await factory.PremiseNameAsync(premiseId));
    }

    [Fact]
    public async Task Options_ReturnsTypesBrandsPrayerRoomsAndStaff()
    {
        using var factory = new PremiseApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedStaffAsync("Nur Aisyah");
        await factory.SeedPrayerRoomAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content
            .ReadFromJsonAsync<PremiseOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal(4, options.PremiseTypes.Count);
        Assert.Contains("CentralKitchen", options.PremiseTypes);
        Assert.Equal("Available",
            Assert.Single(options.PrayerRoomAvailabilities).Name);
        Assert.Equal("Nur Aisyah", Assert.Single(options.Staff).Name);
    }
}
