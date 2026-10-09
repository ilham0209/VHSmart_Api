using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class BatchApiTests
{
    private const string Route = "/api/halal-application/manage-batches";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(BatchApiFactory factory, Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new BatchApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPermission_ReturnsTheSeededRow()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync("SERUNAI");
        await factory.SeedBatchAsync("Santan Batch Pertama", brandId: brandId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllBatchesResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        var row = Assert.Single(payload.Data);
        Assert.Equal("Santan Batch Pertama", row.BatchName);
        Assert.Equal("SERUNAI", row.BrandOwner);
        Assert.Null(row.CbApplicationNo);
        Assert.Equal(1, payload.TotalRecords);
    }

    [Fact]
    public async Task GetAll_ForeignCompanyRow_IsInvisible()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedBatchAsync(companyId: Guid.NewGuid());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllBatchesResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload.Data);
    }

    [Fact]
    public async Task GetOptions_WithPermission_ReturnsSchemesBrandsAndManufacturers()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedBrandAsync("SERUNAI");
        await factory.SeedManufacturerAsync("Santan Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var payload = await ReadAsync<BatchOptionsResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload.Schemes);
        Assert.Contains(payload.Brands, row => row.Name == "SERUNAI");
        Assert.Contains(payload.Manufacturers, row => row.Name == "Santan Foods");
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithPermission_ReturnsTheRow()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var schemeId = await factory.ProductSchemeIdAsync();
        var brandId = await factory.SeedBrandAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route,
            new
            {
                schemeId,
                name = "Batch 271125/1",
                cbReferenceNo = "CB-001",
                submissionPlannedDate = (DateTime?)null,
                brandId,
                description = "First batch"
            },
            Json);
        var payload = await ReadAsync<BatchResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Batch 271125/1", payload.Name);
        Assert.False(payload.IsFoodPremiseScheme);
        Assert.Empty(payload.Products);

        var fetched = await client.GetAsync($"{Route}/{payload.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var schemeId = await factory.ProductSchemeIdAsync();
        var brandId = await factory.SeedBrandAsync();
        await factory.SeedBatchAsync("Santan Batch Pertama", brandId: brandId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route,
            new
            {
                schemeId,
                name = "Santan Batch Pertama",
                cbReferenceNo = (string?)null,
                submissionPlannedDate = (DateTime?)null,
                brandId,
                description = (string?)null
            },
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutBrand_ReturnsBadRequest()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var schemeId = await factory.ProductSchemeIdAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route,
            new
            {
                schemeId,
                name = "Batch Without Brand",
                cbReferenceNo = (string?)null,
                submissionPlannedDate = (DateTime?)null,
                brandId = (Guid?)null,
                description = (string?)null
            },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ProductSchemeWithoutManufacturer_ReturnsBadRequest()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        var batchId = await factory.SeedBatchAsync(brandId: brandId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{batchId}",
            new
            {
                schemeId = await factory.ProductSchemeIdAsync(),
                name = "Santan Batch Pertama",
                cbReferenceNo = (string?)null,
                submissionPlannedDate = (DateTime?)null,
                brandId,
                manufacturerSupplierId = (Guid?)null,
                description = (string?)null
            },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithManufacturer_ReturnsTheRow()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        var manufacturerId = await factory.SeedManufacturerAsync();
        var batchId = await factory.SeedBatchAsync(brandId: brandId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{batchId}",
            new
            {
                schemeId = await factory.ProductSchemeIdAsync(),
                name = "Santan Batch Pertama Edited",
                cbReferenceNo = (string?)null,
                submissionPlannedDate = (DateTime?)null,
                brandId,
                manufacturerSupplierId = manufacturerId,
                description = (string?)null
            },
            Json);
        var payload = await ReadAsync<BatchResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Santan Batch Pertama Edited", payload.Name);
        Assert.Equal(manufacturerId, payload.ManufacturerSupplierId);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContent()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{batchId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = await client.GetAsync($"{Route}/{batchId}");
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LinkProduct_WithValidProduct_ReturnsTheProductRow()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync();
        var productId = await factory.SeedValidProductAsync("Santan Kicap");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/products",
            new { productId },
            Json);
        var payload = await ReadAsync<BatchProductResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(productId, payload.ProductId);
        Assert.Equal("Santan Kicap", payload.ProductName);
        Assert.Equal(BatchProductMappingStatus.Active, payload.MappingStatus);

        var fetched = await client.GetAsync($"{Route}/{batchId}");
        var batch = await ReadAsync<BatchResponse>(fetched);
        Assert.NotNull(batch);
        Assert.Equal(payload.BatchProductId, Assert.Single(batch.Products).BatchProductId);
    }

    [Fact]
    public async Task LinkProduct_WithExpiredProduct_ReturnsUnprocessableEntity()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync();
        var productId = await factory.SeedExpiredProductAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/products",
            new { productId },
            Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task LinkProduct_OnFoodPremiseBatch_ReturnsUnprocessableEntity()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync(
            schemeId: await factory.FoodPremiseSchemeIdAsync());
        var productId = await factory.SeedValidProductAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/products",
            new { productId },
            Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task LinkProduct_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new BatchApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync();
        var productId = await factory.SeedValidProductAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/products",
            new { productId },
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkProduct_ReturnsNoContent()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync();
        var productId = await factory.SeedValidProductAsync();
        using var client = CreateAuthorizedClient(factory);

        var linked = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/products",
            new { productId },
            Json);
        var payload = await ReadAsync<BatchProductResponse>(linked);
        Assert.NotNull(payload);

        var response = await client.DeleteAsync(
            $"{Route}/{batchId}/products/{payload.BatchProductId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The row is kept with INACTIVE (Database.md 10), still on the detail list.
        var fetched = await client.GetAsync($"{Route}/{batchId}");
        var batch = await ReadAsync<BatchResponse>(fetched);
        Assert.NotNull(batch);
        Assert.Equal(
            BatchProductMappingStatus.Inactive,
            Assert.Single(batch.Products).MappingStatus);
    }

    [Fact]
    public async Task ProductOptions_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new BatchApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{batchId}/products/options");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LinkPremise_OnFoodPremiseBatchWithCompletePremise_ReturnsTheRow()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync(
            schemeId: await factory.FoodPremiseSchemeIdAsync());
        var premiseId = await factory.SeedCompletePremiseAsync("PREMISE C");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/premises",
            new { premiseId },
            Json);
        var payload = await ReadAsync<BatchPremiseResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(premiseId, payload.PremiseId);
        Assert.Equal("PREMISE C", payload.PremiseName);
    }

    [Fact]
    public async Task LinkPremise_WithIncompletePremise_ReturnsUnprocessableEntity()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync(
            schemeId: await factory.FoodPremiseSchemeIdAsync());
        var premiseId = await factory.SeedIncompletePremiseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/premises",
            new { premiseId },
            Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkPremise_ReturnsNoContentAndFreesThePair()
    {
        using var factory = new BatchApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var batchId = await factory.SeedBatchAsync(
            schemeId: await factory.FoodPremiseSchemeIdAsync());
        var premiseId = await factory.SeedCompletePremiseAsync();
        using var client = CreateAuthorizedClient(factory);

        var linked = await client.PostAsJsonAsync(
            $"{Route}/{batchId}/premises",
            new { premiseId },
            Json);
        var payload = await ReadAsync<BatchPremiseResponse>(linked);
        Assert.NotNull(payload);

        var response = await client.DeleteAsync(
            $"{Route}/{batchId}/premises/{payload.BatchPremiseId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Soft delete: the detail list drops it and the picker offers the premise again.
        var fetched = await client.GetAsync($"{Route}/{batchId}");
        var batch = await ReadAsync<BatchResponse>(fetched);
        Assert.NotNull(batch);
        Assert.Empty(batch.Premises);

        var options = await client.GetAsync($"{Route}/{batchId}/premises/options");
        var picker = await ReadAsync<DataGridResponse<BatchPremiseOptionResponse>>(options);
        Assert.NotNull(picker);
        Assert.Contains(picker.Data, row => row.Id == premiseId);
    }
}
