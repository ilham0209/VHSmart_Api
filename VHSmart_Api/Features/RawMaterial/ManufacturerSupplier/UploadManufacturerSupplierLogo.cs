using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Manage Manufacturer & Supplier > Logo (spec 10.1 form field): one image per row stored as the
// File column group on RawManufacturerSuppliers (Database.md 8), same flow as the CB logo -
// bytes to IFileStorage first, row second, new file removed if the save fails (CodingRules 10).
// Type and size follow D-22 through FileValidation's image list; content type is derived from
// the extension so the browser is never handed a spoofed MIME.
public record UploadManufacturerSupplierLogoCommand(Guid Id, IFormFile File)
    : IRequest<UploadManufacturerSupplierLogoResponse>;

public record UploadManufacturerSupplierLogoResponse(string FileName, string ContentType, long SizeBytes);

public class UploadManufacturerSupplierLogoValidator
    : AbstractValidator<UploadManufacturerSupplierLogoCommand>
{
    public UploadManufacturerSupplierLogoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.File).NotNull();
    }
}

public class UploadManufacturerSupplierLogoHandler(
    VHSmartDbContext db,
    IFileStorage storage,
    ILogger<UploadManufacturerSupplierLogoHandler> logger)
    : IRequestHandler<UploadManufacturerSupplierLogoCommand, UploadManufacturerSupplierLogoResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<UploadManufacturerSupplierLogoResponse> Handle(
        UploadManufacturerSupplierLogoCommand request,
        CancellationToken ct)
    {
        var entity = await db.ManufacturerSuppliers
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Manufacturer and supplier not found.");

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length, FileValidation.ImageExtensions);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var stored = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var previous = entity.Logo;
        entity.Logo = stored;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        if (previous is not null)
        {
            try
            {
                await storage.DeleteAsync(previous.StorageKey, ct);
            }
            catch (Exception exception)
            {
                // The new logo is already saved; a stale byte file must not fail the request.
                logger.LogWarning(exception, "Could not delete the previous logo {StorageKey}", previous.StorageKey);
            }
        }

        return new UploadManufacturerSupplierLogoResponse(stored.FileName, stored.ContentType, stored.SizeBytes);
    }
}
