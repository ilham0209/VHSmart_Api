using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Personnel.Staff;

// "Staff Attachment" tab upload (spec 7.4: Type of Document from Supporting Document with
// For View = ALL STAFF, Choose File, Upload). The staff id comes from the route and the
// company from the JWT; the document type must be the caller's own company's AdmSupporting-
// Documents row with ForView = AllStaff (Database.md 3), otherwise the form was filled from
// another tenant's catalogue. File rules follow D-22's general list. Bytes are stored before
// the row - a failed save never leaves half a record (CodingRules 10). The same document type
// may be uploaded twice: Database.md documents no uniqueness for this table.
public record UploadStaffAttachmentCommand(
    Guid StaffId,
    Guid DocumentTypeId,
    IFormFile File) : IRequest<StaffAttachmentResponse>;

public class UploadStaffAttachmentValidator : AbstractValidator<UploadStaffAttachmentCommand>
{
    public UploadStaffAttachmentValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.StaffId).NotEmpty();
        RuleFor(x => x.DocumentTypeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Type of document is required.")
            .MustAsync((id, ct) => db.SupportingDocuments.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Type of document not found.")
            .MustAsync((id, ct) => db.SupportingDocuments.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.ForView == SupportingDocumentForView.AllStaff, ct))
            .WithMessage("Type of document is not for All Staff.");
        RuleFor(x => x.File).NotNull().WithMessage("File is required.");
    }
}

public record StaffAttachmentResponse(
    Guid Id,
    Guid StaffId,
    Guid DocumentTypeId,
    string DocumentType,
    string FileName);

public class UploadStaffAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadStaffAttachmentHandler> logger)
    : IRequestHandler<UploadStaffAttachmentCommand, StaffAttachmentResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<StaffAttachmentResponse> Handle(
        UploadStaffAttachmentCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var owned = await db.Staffs.AsNoTracking()
            .AnyAsync(row => row.Id == request.StaffId && row.CompanyId == companyId, ct);
        if (!owned)
            throw new NotFoundException("Staff not found.");

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var document = await storage.SaveAsync(
            file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = new StaffAttachmentEntity
        {
            CompanyId = companyId,
            StaffId = request.StaffId,
            DocumentTypeId = request.DocumentTypeId,
            Document = document
        };
        db.StaffAttachments.Add(entity);

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
                    exception, "Could not remove orphaned staff attachment {StorageKey}", document.StorageKey);
            }

            throw;
        }

        var documentTypeName = await db.SupportingDocuments
            .AsNoTracking()
            .Where(row => row.Id == entity.DocumentTypeId)
            .Select(row => row.DocumentType)
            .SingleAsync(ct);

        return new StaffAttachmentResponse(
            entity.Id, entity.StaffId, entity.DocumentTypeId, documentTypeName, entity.Document.FileName);
    }
}
