using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.CompanyInformation.HalalPolicy;

// Download action of the Halal Policy list (spec 7.2): streams the stored document inline.
// Gated by the same CompanyHalalPolicy View permission as the list (CodingRules 10); the
// explicit CompanyId match keeps a ViewAllCompanies caller on their own rows and answers
// 404 for everything else (CodingRules 9 - never reveal another tenant's row).
public record GetHalalPolicyDocumentQuery(Guid Id) : IRequest<GetHalalPolicyDocumentResponse>;

public record GetHalalPolicyDocumentResponse(Stream Content, string ContentType);

public class GetHalalPolicyDocumentHandler(VHSmartDbContext db, ICurrentUser user, IFileStorage storage)
    : IRequestHandler<GetHalalPolicyDocumentQuery, GetHalalPolicyDocumentResponse>
{
    public async Task<GetHalalPolicyDocumentResponse> Handle(
        GetHalalPolicyDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.HalalPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Halal policy not found.");

        var document = entity.Document;
        if (string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return new GetHalalPolicyDocumentResponse(content, contentType);
    }
}
