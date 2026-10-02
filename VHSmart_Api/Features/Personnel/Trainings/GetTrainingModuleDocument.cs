using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Personnel.Trainings;

// Download of one training module (the Action cell of the spec 7.5 module table): streams the
// stored document inline. Gated by the same Personnel.InternalTraining View permission as the
// list (CodingRules 10); the row must belong to the training in the route and to the caller's
// company. A module stored without bytes (Document is optional, Database.md 6) answers the
// same 404 as missing storage, exactly like the staff attachment download.
public record GetTrainingModuleDocumentQuery(Guid TrainingId, Guid ModuleId)
    : IRequest<GetTrainingModuleDocumentResponse>;

public record GetTrainingModuleDocumentResponse(Stream Content, string ContentType);

public class GetTrainingModuleDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetTrainingModuleDocumentQuery, GetTrainingModuleDocumentResponse>
{
    public async Task<GetTrainingModuleDocumentResponse> Handle(
        GetTrainingModuleDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.TrainingModules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.ModuleId
                    && row.TrainingId == request.TrainingId
                    && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Training module not found.");

        var document = entity.Document;
        if (document is null || string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return new GetTrainingModuleDocumentResponse(content, contentType);
    }
}
