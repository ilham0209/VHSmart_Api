using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class ProductApiTests
{
    private const string Route = "/api/product/manage-products";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(ProductApiFactory factory, Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<CreateProductCommand> CommandForAsync(ProductApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var schemeId = await db.Schemes.AsNoTracking()
            .OrderBy(scheme => scheme.SortOrder)
            .Select(scheme => scheme.Id)
            .FirstAsync();

        return new CreateProductCommand(
            schemeId,
            "Santan Kicap",
            await factory.SeedManufacturerAsync("Santan Foods"),
            await factory.SeedBrandAsync(),
            await factory.SeedProductCategoryAsync(),
            "PRD-001",
            "9551234567890",
            "No MSG",
            "None",
            "120 kcal",
            "Malaysia",
            "250 ml",
            await factory.SeedMarketingMethodAsync());
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new ProductApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new ProductApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPermission_ReturnsTheSeededRow()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllProductsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalRecords);
        Assert.Equal("Santan Kicap", payload.Data.Single().Name);
        Assert.Equal("Sereni", payload.Data.Single().Brand);
        Assert.Equal("Santan Foods", payload.Data.Single().Manufacturer);
    }

    [Fact]
    public async Task GetOptions_ReturnsTheFormDropdowns()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedBrandAsync("Alpha");
        await factory.SeedProductCategoryAsync("Sauces");
        await factory.SeedManufacturerAsync("Santan Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var payload = await ReadAsync<ProductOptionsResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Schemes);
        Assert.Equal("Alpha", payload.Brands.Single().Name);
        Assert.Equal("Sauces", payload.Categories.Single().Name);
        Assert.Equal("Santan Foods", payload.Manufacturers.Single().Name);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_RowOfAnotherCompany_ReturnsNotFound()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync(companyId: Guid.NewGuid());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsTheCreatedRow()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);
        var payload = await ReadAsync<ProductResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Santan Kicap", payload.Name);

        var fetched = await client.GetAsync($"{Route}/{payload.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsBadRequest()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(
            Route,
            command with { Name = null },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_ReturnsUpdatedRow()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);
        var create = await CommandForAsync(factory);
        var command = new UpdateProductCommand(
            id,
            create.SchemeId,
            "Santan Kicap Updated",
            create.ManufacturerSupplierId,
            create.BrandId,
            create.CategoryId,
            create.Code,
            create.Gtin,
            create.NutritionContentClaims,
            create.PotentialAllergens,
            create.CalorieContent,
            create.AvailableAt,
            create.PackagingSize,
            create.MarketingMethodId);

        var response = await client.PutAsJsonAsync($"{Route}/{id}", command, Json);
        var payload = await ReadAsync<ProductResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Santan Kicap Updated", payload.Name);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndHidesTheRow()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        Assert.True((await db.Products.IgnoreQueryFilters().SingleAsync()).IsDeleted);

        var fetched = await client.GetAsync($"{Route}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_WithSelection_ReturnsNoContent()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var first = await factory.SeedRowAsync();
        var second = await factory.SeedRowAsync("Santan Sos", "PRD-002");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete",
            new BulkDeleteProductsCommand([first, second]),
            Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllProductsResponse>>(list);
        Assert.NotNull(payload);
        Assert.Equal(0, payload.TotalRecords);
    }

    [Fact]
    public async Task BulkDelete_EmptySelection_ReturnsBadRequest()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete",
            new BulkDeleteProductsCommand([]),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The Manage Ingredient Information tab (spec 9.1) over HTTP: its routes, the brand rule
    // and the link / unlink halves of the Action icon.
    [Fact]
    public async Task GetIngredients_WithoutViewPermission_ReturnsForbidden()
    {
        using var factory = new ProductApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var productId = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{productId}/ingredients");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetIngredients_CompanyWithoutBrandLink_ReturnsUnprocessableEntity()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var productId = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{productId}/ingredients");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetIngredients_WithBrand_ReturnsTheLinkedRow()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        var productId = await factory.SeedRowAsync();
        var rawMaterialId = await factory.SeedRawMaterialAsync("Rice Flour");
        await factory.SeedHalalCertificateAsync(rawMaterialId, new DateTime(2030, 6, 30));
        await factory.SeedIngredientAsync(productId, rawMaterialId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{productId}/ingredients");
        var payload = await ReadAsync<List<ProductIngredientResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        var row = Assert.Single(payload);
        Assert.Equal("Rice Flour", row.Ingredient);
        Assert.Equal("ACTIVE", row.MappingStatus);
        Assert.Equal(HalalStatus.Valid, row.HalalCertificateInformation.Status);
    }

    [Fact]
    public async Task GetIngredients_UnknownProduct_ReturnsNotFound()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/ingredients");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIngredientOptions_ExcludesTheAlreadyLinkedMaterial()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        var productId = await factory.SeedRowAsync();
        var linkedId = await factory.SeedRawMaterialAsync("Rice Flour");
        await factory.SeedIngredientAsync(productId, linkedId);
        await factory.SeedRawMaterialAsync("Cane Sugar");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{productId}/ingredients/options");
        var payload = await ReadAsync<DataGridResponse<ProductIngredientOptionResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Cane Sugar", Assert.Single(payload.Data).Ingredient);
    }

    [Fact]
    public async Task LinkIngredient_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new ProductApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var productId = await factory.SeedRowAsync();
        var rawMaterialId = await factory.SeedRawMaterialAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{productId}/ingredients",
            new LinkProductIngredientCommand(productId, rawMaterialId),
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LinkIngredient_MissingRawMaterial_ReturnsBadRequest()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        var productId = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{productId}/ingredients",
            new LinkProductIngredientCommand(productId, null),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkAndUnlink_ToggleTheMappingStatusOverOneRow()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        var productId = await factory.SeedRowAsync();
        var rawMaterialId = await factory.SeedRawMaterialAsync("Rice Flour");
        using var client = CreateAuthorizedClient(factory);

        var linked = await client.PostAsJsonAsync(
            $"{Route}/{productId}/ingredients",
            new LinkProductIngredientCommand(productId, rawMaterialId),
            Json);
        var linkedPayload = await ReadAsync<ProductIngredientResponse>(linked);

        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.NotNull(linkedPayload);
        Assert.Equal("ACTIVE", linkedPayload.MappingStatus);
        Assert.Equal("Rice Flour", linkedPayload.Ingredient);

        var unlinked = await client.DeleteAsync(
            $"{Route}/{productId}/ingredients/{linkedPayload.Id}");

        Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.ProductIngredients.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
        Assert.Equal("INACTIVE", stored.MappingStatus);

        var list = await client.GetAsync($"{Route}/{productId}/ingredients");
        var rows = await ReadAsync<List<ProductIngredientResponse>>(list);
        Assert.NotNull(rows);
        Assert.Equal("INACTIVE", Assert.Single(rows).MappingStatus);
    }

    [Fact]
    public async Task GetIngredientCertificate_WithoutCertificate_ReturnsNotFound()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        var productId = await factory.SeedRowAsync();
        var ingredientId = await factory.SeedIngredientAsync(
            productId, await factory.SeedRawMaterialAsync());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"{Route}/{productId}/ingredients/{ingredientId}/certificate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIngredientCertificate_WithCertificate_ReturnsTheFile()
    {
        using var factory = new ProductApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyBrandAsync();
        var productId = await factory.SeedRowAsync();
        var rawMaterialId = await factory.SeedRawMaterialAsync("Rice Flour");
        await factory.SeedHalalCertificateAsync(rawMaterialId, new DateTime(2030, 6, 30));
        var ingredientId = await factory.SeedIngredientAsync(productId, rawMaterialId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"{Route}/{productId}/ingredients/{ingredientId}/certificate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }
}
