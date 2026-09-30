using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// The template of one Supporting Document (spec 5.5, SOP views): streams the stored bytes so
// the form can show/download the file. A download needs the same permission as viewing the
// record (CodingRules 10), so it rides the Admin.SupportingDocuments View key. The tenant
// filter answers 404 for another company's row; a row without a template is 404 as well.
public record GetSupportingDocumentTemplateQuery(Guid Id)
    : IRequest<GetSupportingDocumentTemplateResponse>;

public record GetSupportingDocumentTemplateResponse(Stream Content, string ContentType);

public class GetSupportingDocumentTemplateHandler(VHSmartDbContext db, IFileStorage storage)
    : IRequestHandler<GetSupportingDocumentTemplateQuery, GetSupportingDocumentTemplateResponse>
{
    public async Task<GetSupportingDocumentTemplateResponse> Handle(
        GetSupportingDocumentTemplateQuery request,
        CancellationToken ct)
    {
        var entity = await db.SupportingDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Supporting document not found.");

        var template = entity.Template;
        if (template is null || string.IsNullOrWhiteSpace(template.StorageKey))
            throw new NotFoundException("No template.");

        var content = await storage.OpenReadAsync(template.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(template.ContentType)
            ? "application/octet-stream"
            : template.ContentType;

        return new GetSupportingDocumentTemplateResponse(content, contentType);
    }
}
