using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// D-24: "today" for Document Status and expiry checks is the Asia/Kuala_Lumpur (UTC+8) local
// date, not the stored UTC timestamp. The offset is fixed (Malaysia has no DST).
internal static class PremiseDocumentClock
{
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
}

// Tab "Premise Attachment" of the View/Edit Premise modal (spec 7.7): always the five D-15
// required types in order; a type with no row answers an "N/A" placeholder (null Id/FileName)
// instead of being omitted, exactly like the screenshot. Rows that exist show their file and
// a per-row Document Status of Valid / Expired (null = untouched N/A - the legacy rule "expiry
// can be updated without re-uploading" [CODE] is covered by re-uploading through the same
// endpoint; a dedicated edit endpoint is not in this task). The premise must belong to the
// caller's company (404 otherwise).
public record GetPremiseAttachmentsQuery(Guid PremiseId)
    : IRequest<IReadOnlyList<PremiseAttachmentResponse>>;

public record PremiseAttachmentResponse(
    Guid? Id,
    int No,
    string DocumentType,
    string? FileName,
    DateOnly? ExpiryDate,
    string? ReferenceNo,
    HalalStatus? DocumentStatus);

public class GetPremiseAttachmentsHandler(
    VHSmartDbContext db,
    ICurrentUser user)
    : IRequestHandler<GetPremiseAttachmentsQuery, IReadOnlyList<PremiseAttachmentResponse>>
{
    public async Task<IReadOnlyList<PremiseAttachmentResponse>> Handle(
        GetPremiseAttachmentsQuery request,
        CancellationToken ct) =>
        await PremiseAttachmentListLoader.LoadAsync(db, user.CompanyId, request.PremiseId, ct);
}

internal static class PremiseAttachmentListLoader
{
    // One code path for GET and for the list returned after an upload (same reasoning as the
    // other detail loaders). The rows are matched onto the D-15 list case-insensitively, so a
    // legacy value in any casing still lines up with its "N/A" slot.
    public static async Task<IReadOnlyList<PremiseAttachmentResponse>> LoadAsync(
        VHSmartDbContext db,
        Guid companyId,
        Guid premiseId,
        CancellationToken ct)
    {
        var premiseExists = await db.Premises.AsNoTracking()
            .AnyAsync(row => row.Id == premiseId && row.CompanyId == companyId, ct);
        if (!premiseExists)
            throw new NotFoundException("Premise not found.");

        var rows = await db.PremiseAttachments.AsNoTracking()
            .Where(row => row.PremiseId == premiseId)
            .ToListAsync(ct);
        var rowsByType = rows
            .GroupBy(row => row.DocumentType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var today = PremiseDocumentClock.Today();
        var responses = new List<PremiseAttachmentResponse>(PremiseDocumentStatusCalculator.RequiredDocumentTypes.Count);
        var no = 0;
        foreach (var documentType in PremiseDocumentStatusCalculator.RequiredDocumentTypes)
        {
            no++;
            if (!rowsByType.TryGetValue(documentType, out var row))
            {
                responses.Add(new PremiseAttachmentResponse(
                    Id: null,
                    No: no,
                    DocumentType: documentType,
                    FileName: null,
                    ExpiryDate: null,
                    ReferenceNo: null,
                    DocumentStatus: null));
                continue;
            }

            // D-04: the 9999-12-31 sentinel means "no expiry" and is displayed empty.
            var expiryDate = HalalStatusCalculator.ExpiryForDisplay(
                row.ExpiryDate is null ? null : DateOnly.FromDateTime(row.ExpiryDate.Value));
            responses.Add(new PremiseAttachmentResponse(
                row.Id,
                no,
                documentType,
                row.Document.FileName,
                expiryDate,
                row.ReferenceNo,
                HalalStatusCalculator.Status(expiryDate, today)));
        }

        return responses;
    }
}
