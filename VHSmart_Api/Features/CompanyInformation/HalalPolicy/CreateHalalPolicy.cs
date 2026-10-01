using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.CompanyInformation.HalalPolicy;

// Add a Halal Policy (spec 7.2 modal "Manage Halal Policy": Scheme*, Upload Company Halal
// Policy*, Halal Policy Date*): one multipart form, the row is written only after the bytes are
// stored - a failed save never leaves a half-created record (CodingRules 10). One policy per
// scheme per company (Database.md 3, spec 7.2 [VERIFY] default "enforce") -> handler 409; the
// filtered UQ index is the backstop on SQL Server. CompanyId comes from the JWT only. File
// rules follow D-22's general list (the spec saw .docx); messages are ours (spec gives none).
public record CreateHalalPolicyCommand(
    Guid SchemeId,
    DateTime PolicyDate,
    IFormFile File) : IRequest<HalalPolicyResponse>;

public class CreateHalalPolicyValidator : AbstractValidator<CreateHalalPolicyCommand>
{
    public CreateHalalPolicyValidator(VHSmartDbContext db)
    {
        RuleFor(x => x.SchemeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Scheme is required.")
            .MustAsync((id, ct) => db.Schemes.AnyAsync(scheme => scheme.Id == id, ct))
            .WithMessage("Scheme not found.");
        RuleFor(x => x.PolicyDate)
            .NotEmpty().WithMessage("Policy date is required.");
        RuleFor(x => x.File).NotNull().WithMessage("Halal policy document is required.");
    }
}

public record HalalPolicyResponse(
    Guid Id,
    Guid SchemeId,
    string Scheme,
    DateTime PolicyDate,
    string FileName);

public class CreateHalalPolicyHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<CreateHalalPolicyHandler> logger)
    : IRequestHandler<CreateHalalPolicyCommand, HalalPolicyResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<HalalPolicyResponse> Handle(
        CreateHalalPolicyCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var duplicate = await db.HalalPolicies.AnyAsync(row =>
            row.CompanyId == companyId && row.SchemeId == request.SchemeId, ct);
        if (duplicate)
            throw new ConflictException("A halal policy for this scheme already exists.");

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var document = await storage.SaveAsync(
            file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = new HalalPolicyEntity
        {
            CompanyId = companyId,
            SchemeId = request.SchemeId,
            PolicyDate = request.PolicyDate,
            Document = document
        };
        db.HalalPolicies.Add(entity);

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
                    exception, "Could not remove orphaned halal policy {StorageKey}", document.StorageKey);
            }

            throw;
        }

        var schemeName = await db.Schemes
            .AsNoTracking()
            .Where(scheme => scheme.Id == entity.SchemeId)
            .Select(scheme => scheme.Name)
            .SingleAsync(ct);

        return new HalalPolicyResponse(
            entity.Id, entity.SchemeId, schemeName, entity.PolicyDate, entity.Document.FileName);
    }
}
