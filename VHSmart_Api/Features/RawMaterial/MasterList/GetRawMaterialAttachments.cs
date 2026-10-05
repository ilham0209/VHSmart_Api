using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Section "Attachment Information" of the "Manage Master Raw Material" modal (spec 10.2): one
// row per the company's Supporting Document type with For View = Raw Material (R-06), in
// document-sequence order, each carrying its upload or an "N/A" placeholder (null Id / null
// File Name) until one exists - the shape of the screenshot. Expiry Date and Document Status
// follow D-04: null and the 9999-12-31 sentinel are "no expiry", shown empty and never
// Expired. The raw material must be visible to the caller under the 7.3 filter, so an unknown
// or foreign id answers 404 (never 403).
public record GetRawMaterialAttachmentsQuery(Guid RawMaterialId)
    : IRequest<IReadOnlyList<RawMaterialAttachmentResponse>>;

public record RawMaterialAttachmentResponse(
    Guid? Id,
    Guid DocumentTypeId,
    string DocumentType,
    string? FileName,
    DateOnly? ExpiryDate,
    HalalStatus? DocumentStatus,
    string? ReferenceNo,
    string? Authority);

public class GetRawMaterialAttachmentsHandler(VHSmartDbContext db)
    : IRequestHandler<GetRawMaterialAttachmentsQuery, IReadOnlyList<RawMaterialAttachmentResponse>>
{
    public async Task<IReadOnlyList<RawMaterialAttachmentResponse>> Handle(
        GetRawMaterialAttachmentsQuery request,
        CancellationToken ct) =>
        await RawMaterialAttachmentListLoader.LoadAsync(db, request.RawMaterialId, ct);
}

// One code path for GET and for the list returned after an upload (the same reasoning as the
// other detail loaders of this codebase).
internal static class RawMaterialAttachmentListLoader
{
    public static async Task<IReadOnlyList<RawMaterialAttachmentResponse>> LoadAsync(
        VHSmartDbContext db,
        Guid rawMaterialId,
        CancellationToken ct)
    {
        // The visibility filter (CodingRules 7.3) is what makes an unknown, foreign or
        // soft-deleted row 404 here - the very check the detail read makes.
        var rawMaterialExists = await db.RawMaterials.AsNoTracking()
            .AnyAsync(row => row.Id == rawMaterialId, ct);
        if (!rawMaterialExists)
            throw new NotFoundException("Raw material not found.");

        var types = await db.SupportingDocuments.AsNoTracking()
            .Where(row => row.ForView == SupportingDocumentForView.RawMaterial)
            .ToListAsync(ct);
        var attachments = await db.RawMaterialAttachments.AsNoTracking()
            .Where(row => row.RawMaterialId == rawMaterialId)
            .ToListAsync(ct);
        var byType = attachments
            .GroupBy(row => row.DocumentTypeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.SysDateCreated).First());

        var today = RawMaterialHalalInformation.Today();
        var responses = new List<RawMaterialAttachmentResponse>(types.Count);
        foreach (var type in types
            .OrderBy(row => row.DocumentSequence)
            .ThenBy(row => row.DocumentType, StringComparer.Ordinal))
        {
            if (!byType.TryGetValue(type.Id, out var row))
            {
                responses.Add(new RawMaterialAttachmentResponse(
                    Id: null,
                    DocumentTypeId: type.Id,
                    DocumentType: type.DocumentType,
                    FileName: null,
                    ExpiryDate: null,
                    DocumentStatus: null,
                    ReferenceNo: null,
                    Authority: null));
                continue;
            }

            // D-04: the sentinel (and a missing date) mean "no expiry" - empty, never expired.
            var expiryDate = HalalStatusCalculator.ExpiryForDisplay(
                row.ExpiryDate is null ? null : DateOnly.FromDateTime(row.ExpiryDate.Value));

            responses.Add(new RawMaterialAttachmentResponse(
                row.Id,
                type.Id,
                type.DocumentType,
                row.Document.FileName,
                expiryDate,
                HalalStatusCalculator.Status(expiryDate, today),
                row.ReferenceNo,
                row.Authority));
        }

        return responses;
    }
}
