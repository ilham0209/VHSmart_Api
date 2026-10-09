using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Attachment side tab of the application screen (spec 12.5): "Halal Application Supporting
// Document" - one row per the company's own Supporting Document type with For View =
// Halal Application (R-06), in document-sequence order, each carrying its upload or an N/A
// placeholder (null Id / null File Name) until one exists - the same shape the Raw Material
// Attachment Information section answers, so the same endpoint also feeds the dialog's
// Document Type dropdown. Unknown or foreign application -> 404 (never 403).
public record GetApplicationAttachmentsQuery(Guid ApplicationId)
    : IRequest<IReadOnlyList<ApplicationAttachmentResponse>>;

public record ApplicationAttachmentResponse(
    Guid? Id,
    Guid DocumentTypeId,
    string DocumentType,
    string? FileName);

public class GetApplicationAttachmentsHandler(VHSmartDbContext db)
    : IRequestHandler<GetApplicationAttachmentsQuery, IReadOnlyList<ApplicationAttachmentResponse>>
{
    public async Task<IReadOnlyList<ApplicationAttachmentResponse>> Handle(
        GetApplicationAttachmentsQuery request,
        CancellationToken ct) =>
        await ApplicationAttachmentListLoader.LoadAsync(db, request.ApplicationId, ct);
}

// One code path for GET and for the list returned after an upload (the same reasoning as the
// other detail loaders of this codebase).
internal static class ApplicationAttachmentListLoader
{
    public static async Task<IReadOnlyList<ApplicationAttachmentResponse>> LoadAsync(
        VHSmartDbContext db,
        Guid applicationId,
        CancellationToken ct)
    {
        // The visibility filter (CodingRules 7.3) is what makes an unknown, foreign or
        // soft-deleted application 404 here - the very check every other tab makes.
        var applicationExists = await db.Applications.AsNoTracking()
            .AnyAsync(row => row.Id == applicationId, ct);
        if (!applicationExists)
            throw new NotFoundException("Application not found.");

        var types = await db.SupportingDocuments.AsNoTracking()
            .Where(row => row.ForView == SupportingDocumentForView.HalalApplication)
            .ToListAsync(ct);
        var attachments = await db.ApplicationAttachments.AsNoTracking()
            .Where(row => row.ApplicationId == applicationId)
            .ToListAsync(ct);
        var byType = attachments
            .GroupBy(row => row.DocumentTypeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.SysDateCreated).First());

        var responses = new List<ApplicationAttachmentResponse>(types.Count);
        foreach (var type in types
            .OrderBy(row => row.DocumentSequence)
            .ThenBy(row => row.DocumentType, StringComparer.Ordinal))
        {
            responses.Add(byType.TryGetValue(type.Id, out var row)
                ? new ApplicationAttachmentResponse(
                    row.Id, type.Id, type.DocumentType, row.Document.FileName)
                : new ApplicationAttachmentResponse(
                    null, type.Id, type.DocumentType, null));
        }

        return responses;
    }
}
