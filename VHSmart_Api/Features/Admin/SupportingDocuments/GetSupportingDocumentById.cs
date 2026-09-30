using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// The view/edit form of one Supporting Document (spec 5.5): every form field plus the template
// file name (the bytes come from GET {id}/template). The tenant filter answers 404 for another
// company's row. Shared by Get / Create / Update like WebLinkResponse is.
public record GetSupportingDocumentByIdQuery(Guid Id) : IRequest<SupportingDocumentResponse>;

public record SupportingDocumentResponse(
    Guid Id,
    SupportingDocumentForView ForView,
    string DocumentType,
    int DocumentSequence,
    bool IsMandatory,
    string? Description,
    string? TemplateFileName,
    DateTime? ModifiedDate)
{
    internal static SupportingDocumentResponse From(SupportingDocumentEntity entity) =>
        new(
            entity.Id,
            entity.ForView,
            entity.DocumentType,
            entity.DocumentSequence,
            entity.IsMandatory,
            entity.Description,
            entity.Template?.FileName,
            entity.SysDateModified);
}

public class GetSupportingDocumentByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetSupportingDocumentByIdQuery, SupportingDocumentResponse>
{
    public async Task<SupportingDocumentResponse> Handle(
        GetSupportingDocumentByIdQuery request,
        CancellationToken ct)
    {
        var entity = await db.SupportingDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Supporting document not found.");

        return SupportingDocumentResponse.From(entity);
    }
}
