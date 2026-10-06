using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Shared.Infrastructure.Storage;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

// Tab "Manage Attachment Information" (spec 9.1, Database.md 9, D-22): the four angles, the
// document type labels, the exactly-1200 x 1200 rule, the version counter with IsCurrent, the
// streamed picture and the delete.
public class ProductImageTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartProductImageTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public ProductImageTests() => _storage = new LocalFileStorage(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static IFormFile ImageFile(
        byte[]? bytes = null,
        string fileName = "front.png")
    {
        var content = bytes ?? TestImages.Png(1200, 1200);
        return new FormFile(new MemoryStream(content), 0, content.Length, "File", fileName);
    }

    private static UploadProductImageCommand UploadCommand(
        Guid productId,
        string? documentType = "PRODUCT IMAGES (FRONT)",
        IFormFile? file = null) =>
        new(productId, documentType, file ?? ImageFile());

    private UploadProductImageHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadProductImageHandler>.Instance);

    private GetProductImageHandler StreamHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage);

    private static async Task<IReadOnlyList<ProductImageResponse>> ListAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Guid productId) =>
        await new GetProductImagesHandler(db, user)
            .Handle(new GetProductImagesQuery(productId), CancellationToken.None);

    [Fact]
    public async Task List_UnknownOrForeignProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignId = await SeedProductAsync(db, companyId: CompanyB);
        var handler = new GetProductImagesHandler(db, UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProductImagesQuery(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProductImagesQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task List_ShowsTheCurrentRowOfEachAngle_InGuidelineOrder()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedProductAsync(db);

        // Seeded out of order on purpose: the table follows the guidelines box, not the ids.
        await SeedProductImageAsync(db, productId, ProductImagePosition.Right, version: 1);
        await SeedProductImageAsync(db, productId, ProductImagePosition.Left, version: 3);
        await SeedProductImageAsync(db, productId, ProductImagePosition.Back, version: 2);
        await SeedProductImageAsync(db, productId, ProductImagePosition.Front, version: 4);
        // A superseded version is history: it must never appear on the tab.
        await SeedProductImageAsync(
            db, productId, ProductImagePosition.Front, version: 3, isCurrent: false,
            fileName: "front-old.png");

        var response = await ListAsync(db, UserA(), productId);

        Assert.Equal(
            new[] { ProductImagePosition.Front, ProductImagePosition.Back, ProductImagePosition.Left, ProductImagePosition.Right },
            response.Select(row => row.Position).ToArray());
        Assert.Equal(
            new[] { "PRODUCT IMAGES (FRONT)", "PRODUCT IMAGES (BACK)", "PRODUCT IMAGES (LEFT)", "PRODUCT IMAGES (RIGHT)" },
            response.Select(row => row.DocumentType).ToArray());
        Assert.Equal(4, response.Count);
        Assert.Equal(4, response[0].Version);
        Assert.Equal("front.png", response[0].DocumentName);
    }

    [Fact]
    public async Task List_EmptyAngle_SimplyHasNoRow()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedProductAsync(db);

        var response = await ListAsync(db, UserA(), productId);

        // No placeholder rows: the tab lists what was uploaded (the spec's guidelines ask for
        // "at least two (2) or four (4) angles", so an empty angle is normal).
        Assert.Empty(response);
    }

    [Fact]
    public async Task Upload_FirstImageOfAnAngle_StoresTheRowAndTheBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);

        var response = await UploadHandler(db, user).Handle(
            UploadCommand(productId, file: ImageFile(fileName: "front.png")),
            CancellationToken.None);

        var uploaded = Assert.Single(response);
        Assert.Equal(ProductImagePosition.Front, uploaded.Position);
        Assert.Equal("PRODUCT IMAGES (FRONT)", uploaded.DocumentType);
        Assert.Equal("front.png", uploaded.DocumentName);
        Assert.Equal(1, uploaded.Version);

        var stored = await db.ProductImages.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(productId, stored.ProductId);
        Assert.True(stored.IsCurrent);
        Assert.Equal("image/png", stored.Image.ContentType);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Image.StorageKey)));
    }

    [Fact]
    public async Task Upload_TheFourAngles_AreIndependentRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var handler = UploadHandler(db, user);

        await handler.Handle(
            UploadCommand(productId, "PRODUCT IMAGES (FRONT)"), CancellationToken.None);
        await handler.Handle(
            UploadCommand(productId, "back", file: ImageFile(fileName: "back.png")),
            CancellationToken.None);
        await handler.Handle(
            UploadCommand(productId, "Left", file: ImageFile(fileName: "left.png")),
            CancellationToken.None);
        await handler.Handle(
            UploadCommand(productId, "PRODUCT IMAGES (RIGHT)", file: ImageFile(fileName: "right.png")),
            CancellationToken.None);

        var response = await ListAsync(db, user, productId);

        Assert.Equal(4, response.Count);
        Assert.All(response, row => Assert.Equal(1, row.Version));
        Assert.Equal(
            new[] { "front.png", "back.png", "left.png", "right.png" },
            response.Select(row => row.DocumentName).ToArray());
    }

    [Fact]
    public async Task Upload_SameAngleAgain_KeepsTheOldRowAndBumpsTheVersion()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var handler = UploadHandler(db, user);

        await handler.Handle(UploadCommand(productId), CancellationToken.None);
        await handler.Handle(
            UploadCommand(productId, file: ImageFile(fileName: "front-v2.png")),
            CancellationToken.None);

        // Database.md 9: the superseded row is kept with IsCurrent = false, the new row carries
        // Version + 1 and IsCurrent = true.
        Assert.Equal(2, await db.ProductImages.IgnoreQueryFilters().CountAsync());
        var history = await db.ProductImages.IgnoreQueryFilters()
            .Where(row => !row.IsCurrent)
            .SingleAsync();
        Assert.Equal(1, history.Version);
        Assert.Equal("front.png", history.Image.FileName);

        var current = await db.ProductImages.SingleAsync(row => row.IsCurrent);
        Assert.Equal(2, current.Version);
        Assert.True(current.IsCurrent);
        Assert.Equal("front-v2.png", current.Image.FileName);

        var response = await ListAsync(db, user, productId);
        var only = Assert.Single(response);
        Assert.Equal(2, only.Version);
        Assert.Equal("front-v2.png", only.DocumentName);
    }

    [Fact]
    public async Task Upload_AfterADelete_NeverReusesTheVersionNumber()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var handler = UploadHandler(db, user);

        await handler.Handle(UploadCommand(productId), CancellationToken.None);
        await handler.Handle(
            UploadCommand(productId, file: ImageFile(fileName: "front-v2.png")),
            CancellationToken.None);

        var currentId = (await db.ProductImages.SingleAsync(row => row.IsCurrent)).Id;
        await new DeleteProductImageHandler(db, user)
            .Handle(new DeleteProductImageCommand(productId, currentId), CancellationToken.None);

        var response = await handler.Handle(
            UploadCommand(productId, file: ImageFile(fileName: "front-v3.png")),
            CancellationToken.None);

        // 1 -> 2 -> (deleted) -> 3: the number of a deleted row is never handed out again.
        var only = Assert.Single(response);
        Assert.Equal(3, only.Version);
        Assert.Equal("front-v3.png", only.DocumentName);
    }

    [Fact]
    public async Task Upload_UnknownOrForeignProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignId = await SeedProductAsync(db, companyId: CompanyB);
        var handler = UploadHandler(db, UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(UploadCommand(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(UploadCommand(foreignId), CancellationToken.None));

        Assert.Equal(0, await db.ProductImages.IgnoreQueryFilters().CountAsync());
        Assert.False(Directory.Exists(_storageRoot) && Directory.EnumerateFiles(_storageRoot).Any());
    }

    [Fact]
    public async Task Upload_BarePositionAndLabel_BothMapToTheSameAngle()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var handler = UploadHandler(db, user);

        await handler.Handle(
            UploadCommand(productId, "front", file: ImageFile(fileName: "a.png")),
            CancellationToken.None);
        await handler.Handle(
            UploadCommand(productId, "PRODUCT IMAGES (front)", file: ImageFile(fileName: "b.png")),
            CancellationToken.None);

        var response = await ListAsync(db, user, productId);
        var only = Assert.Single(response);
        Assert.Equal(ProductImagePosition.Front, only.Position);
        Assert.Equal(2, only.Version);
    }

    [Fact]
    public async Task Validator_DocumentTypeAndFile_AreRequiredOrRejected()
    {
        var validator = new UploadProductImageValidator();

        var missingType = await validator.ValidateAsync(
            new UploadProductImageCommand(Guid.NewGuid(), null, ImageFile()));
        Assert.False(missingType.IsValid);
        Assert.Contains(
            missingType.Errors,
            failure => failure.ErrorMessage == "Document type is required.");

        var unknownType = await validator.ValidateAsync(
            new UploadProductImageCommand(Guid.NewGuid(), "PRODUCT IMAGES (TOP)", ImageFile()));
        Assert.False(unknownType.IsValid);
        Assert.Contains(
            unknownType.Errors,
            failure => failure.ErrorMessage == "Document type not found.");

        var missingFile = await validator.ValidateAsync(
            new UploadProductImageCommand(Guid.NewGuid(), "PRODUCT IMAGES (FRONT)", null));
        Assert.False(missingFile.IsValid);
        Assert.Contains(
            missingFile.Errors,
            failure => failure.ErrorMessage == "File is required.");

        Assert.True((await validator.ValidateAsync(UploadCommand(Guid.NewGuid()))).IsValid);
    }

    [Fact]
    public async Task Upload_DisallowedFileType_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(productId, file: ImageFile(fileName: "front.gif")),
                CancellationToken.None));

        Assert.Equal(0, await db.ProductImages.IgnoreQueryFilters().CountAsync());
        Assert.False(Directory.Exists(_storageRoot) && Directory.EnumerateFiles(_storageRoot).Any());
    }

    [Fact]
    public async Task Upload_OversizeFile_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var oversize = new FormFile(
            new MemoryStream([1]), 0, (10 * 1024 * 1024) + 1, "File", "huge.png");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(productId, file: oversize),
                CancellationToken.None));

        Assert.Equal(0, await db.ProductImages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Upload_WrongPixelSize_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(productId, file: ImageFile(TestImages.Png(800, 800))),
                CancellationToken.None));

        Assert.Equal("The image must be exactly 1200 x 1200 pixels.", exception.Message);
        Assert.Equal(0, await db.ProductImages.IgnoreQueryFilters().CountAsync());
        Assert.False(Directory.Exists(_storageRoot) && Directory.EnumerateFiles(_storageRoot).Any());
    }

    [Fact]
    public async Task Upload_BytesThatAreNotAnImage_ThrowBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(productId, file: ImageFile("not a picture"u8.ToArray())),
                CancellationToken.None));

        Assert.Equal("The file is not a valid image.", exception.Message);
        Assert.Equal(0, await db.ProductImages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Delete_ExistingRow_SoftDeletesItAndEmptiesTheAngle()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        await UploadHandler(db, user).Handle(UploadCommand(productId), CancellationToken.None);
        var imageId = (await db.ProductImages.SingleAsync()).Id;

        await new DeleteProductImageHandler(db, user)
            .Handle(new DeleteProductImageCommand(productId, imageId), CancellationToken.None);

        // Soft delete only (CodingRules 7.1): the row stays, the tab stops showing it.
        Assert.Empty(await ListAsync(db, user, productId));
        var deleted = await db.ProductImages.IgnoreQueryFilters().SingleAsync();
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task Delete_UnknownMismatchedOrForeignRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var otherProductId = await SeedProductAsync(db, name: "Other Product");
        await UploadHandler(db, user).Handle(UploadCommand(productId), CancellationToken.None);
        var imageId = (await db.ProductImages.SingleAsync()).Id;
        var handler = new DeleteProductImageHandler(db, user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeleteProductImageCommand(productId, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeleteProductImageCommand(otherProductId, imageId), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeleteProductImageCommand(Guid.NewGuid(), imageId), CancellationToken.None));

        Assert.Equal(1, await db.ProductImages.CountAsync());
    }

    [Fact]
    public async Task Document_StreamsTheStoredFile()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        await UploadHandler(db, user).Handle(UploadCommand(productId), CancellationToken.None);
        var imageId = (await db.ProductImages.SingleAsync()).Id;

        var response = await StreamHandler(db, user).Handle(
            new GetProductImageQuery(productId, imageId), CancellationToken.None);

        // The stream is a file handle on the storage root: close it before the fixture cleans
        // the directory up.
        using var content = response.Content;
        Assert.Equal("image/png", response.ContentType);
        var bytes = new byte[8];
        var read = await content.ReadAsync(bytes);
        Assert.Equal(8, read);
        // The stored picture is the very PNG the upload was validated against.
        Assert.Equal(TestImages.Png(1200, 1200).AsSpan(0, 8).ToArray(), bytes);
    }

    [Fact]
    public async Task Document_UnknownMismatchedOrForeignId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var otherProductId = await SeedProductAsync(db, name: "Other Product");
        await UploadHandler(db, user).Handle(UploadCommand(productId), CancellationToken.None);
        var imageId = (await db.ProductImages.SingleAsync()).Id;
        var handler = StreamHandler(db, user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProductImageQuery(productId, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProductImageQuery(otherProductId, imageId), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProductImageQuery(Guid.NewGuid(), imageId), CancellationToken.None));
    }
}
