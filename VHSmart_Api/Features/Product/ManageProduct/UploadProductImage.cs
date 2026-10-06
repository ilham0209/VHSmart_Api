using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Product.ManageProduct;

// Tab "Manage Attachment Information" upload (spec 9.1): Document Type* + Choose File. The four
// angles of Database.md 9 are Front / Back / Left / Right, so one upload always belongs to
// exactly one angle of one product. D-22 is enforced server side (jpg / jpeg / png, 10 MB, and
// EXACTLY 1200 x 1200 px) before anything is stored - the legacy system checked the size in the
// browser only. Replacing an angle keeps the superseded row (IsCurrent = false) and adds a new
// one with Version + 1 and IsCurrent = true, which is what Database.md 9 prescribes. Bytes are
// stored first, the row second (CodingRules 10), so a failed save never leaves a half-created
// record. Only the owning company may upload: a foreign product answers 404, never 403.
public record UploadProductImageCommand(Guid ProductId, string? DocumentType, IFormFile? File)
    : IRequest<IReadOnlyList<ProductImageResponse>>;

public class UploadProductImageValidator : AbstractValidator<UploadProductImageCommand>
{
    public UploadProductImageValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();

        RuleFor(x => x.DocumentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Document type is required.")
            .Must(value => ProductImageData.TryParseDocumentType(value, out _))
            .WithMessage("Document type not found.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required.");
    }
}

public class UploadProductImageHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadProductImageHandler> logger)
    : IRequestHandler<UploadProductImageCommand, IReadOnlyList<ProductImageResponse>>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<IReadOnlyList<ProductImageResponse>> Handle(
        UploadProductImageCommand request,
        CancellationToken ct)
    {
        var product = await ProductImageData.GetProductAsync(db, user, request.ProductId, ct);

        // The validator answers first, but a handler must never default a document type it
        // cannot map to one of the four angles.
        if (!ProductImageData.TryParseDocumentType(request.DocumentType, out var position))
            throw new BusinessRuleException("Document type not found.");

        if (request.File is not { } file)
            throw new BusinessRuleException("File is required.");

        FileValidation.Validate(file.FileName, file.Length, FileValidation.ImageExtensions);

        // Read once: the pixel size is checked from these bytes and the same bytes are stored,
        // so the picture is never opened twice.
        byte[] bytes;
        using (var input = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await input.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        ProductImageData.ValidateDimensions(bytes);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        StoredFile document;
        using (var content = new MemoryStream(bytes))
        {
            document = await storage.SaveAsync(content, file.FileName, contentType, ct);
        }

        // The superseded row of this angle is kept as history (Database.md 9), so the version
        // number must survive it: the next number is the highest ever used for this angle,
        // including rows a delete has already hidden. IgnoreQueryFilters - a uniqueness-style
        // read that must see soft-deleted rows (CodingRules 7.3); the product id scopes it to
        // this product, so no other tenant's row can be reached.
        var lastVersion = await db.ProductImages.IgnoreQueryFilters()
            .Where(row => row.ProductId == product.Id && row.Position == position)
            .MaxAsync(row => (int?)row.Version, ct) ?? 0;

        var current = await db.ProductImages
            .FirstOrDefaultAsync(
                row => row.ProductId == product.Id
                    && row.Position == position
                    && row.IsCurrent,
                ct);

        if (current is not null)
            current.IsCurrent = false;

        var entity = new ProductImageEntity
        {
            // The row's own company, not the caller's: the image belongs with the product.
            CompanyId = product.CompanyId,
            ProductId = product.Id,
            Position = position,
            Version = lastVersion + 1,
            IsCurrent = true,
            Image = document
        };
        db.ProductImages.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row was rejected: drop the bytes we just wrote. A storage failure here must
            // not hide the original save error, so it is only logged.
            try
            {
                await storage.DeleteAsync(document.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not remove orphaned product image {StorageKey}",
                    document.StorageKey);
            }

            throw;
        }

        return await ProductImageListLoader.LoadAsync(db, user, product.Id, ct);
    }
}
