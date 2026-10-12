using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Audit.NonConformanceList;

// "Fill by Auditee" save of the Manage Corrective Action Details page (spec 14.11
// [MANUAL]): Corrective Action Details* plus an optional Attachment (only Details carries
// the required marker on the form). Every save keeps a versioned append-only entry
// (Database.md 13) numbered per non-conformance across both sides. D-05: the save moves
// Open -> CorrectiveActionSubmitted; a re-save while submitted just adds a version; Closed
// is a 409 (reopen not allowed). The file follows D-22 (10 MB, pdf/docx/xlsx/jpg/jpeg/png)
// and the staff-attachment pattern: bytes to IFileStorage first, row second, the stored
// file dropped again if the save fails (CodingRules 10). FilledByUserId comes from the
// JWT. Behind Audit.NonConformance Edit (D-19 gives the premise manager Edit for the
// corrective action).
public record SaveCorrectiveActionCommand(
    Guid Id,
    string? Details,
    IFormFile? File) : IRequest<CorrectiveActionEntryResponse>;

public record CorrectiveActionEntryResponse(
    Guid Id,
    Guid NonConformanceId,
    string FilledBy,
    string? Details,
    string? Comment,
    string? StatusSet,
    int Version,
    string? DocumentName,
    long? DocumentSizeBytes);

public class SaveCorrectiveActionValidator : AbstractValidator<SaveCorrectiveActionCommand>
{
    public SaveCorrectiveActionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("Corrective Action Details is required.")
            .MaximumLength(2000)
            .WithMessage("Corrective Action Details cannot exceed 2000 characters.");
    }
}

public class SaveCorrectiveActionHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<SaveCorrectiveActionCommand, CorrectiveActionEntryResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<CorrectiveActionEntryResponse> Handle(
        SaveCorrectiveActionCommand request,
        CancellationToken ct)
    {
        var nc = await NonConformanceQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Non-conformance not found.");

        if (nc.Status == NonConformanceStatus.Closed)
        {
            throw new ConflictException("The non-conformance is closed.");
        }

        if (!Guid.TryParse(user.UserId, out var userId))
        {
            throw new BusinessRuleException("User id is invalid.");
        }

        StoredFile? storedFile = null;
        if (request.File is not null)
        {
            FileValidation.Validate(request.File.FileName, request.File.Length);
            await using var stream = request.File.OpenReadStream();
            storedFile = await storage.SaveAsync(
                stream,
                request.File.FileName,
                ContentTypes.TryGetContentType(request.File.FileName, out var contentType)
                    ? contentType
                    : "application/octet-stream",
                ct);
        }

        try
        {
            var version = await NextVersionAsync(db, nc.Id, ct);
            var entry = new CorrectiveActionEntryEntity
            {
                CompanyId = user.CompanyId,
                NonConformanceId = nc.Id,
                FilledBy = CorrectiveActionFilledBy.Auditee,
                Details = request.Details!,
                Version = version,
                Attachment = storedFile,
                FilledByUserId = userId
            };
            db.CorrectiveActionEntries.Add(entry);

            nc.Status = NonConformanceStatus.CorrectiveActionSubmitted;

            await db.SaveChangesAsync(ct);

            return new CorrectiveActionEntryResponse(
                entry.Id,
                nc.Id,
                entry.FilledBy.ToString(),
                entry.Details,
                entry.Comment,
                entry.StatusSet,
                entry.Version,
                entry.Attachment?.FileName,
                entry.Attachment?.SizeBytes);
        }
        catch
        {
            if (storedFile is not null)
            {
                await storage.DeleteAsync(storedFile.StorageKey, ct);
            }

            throw;
        }
    }

    internal static async Task<int> NextVersionAsync(
        VHSmartDbContext db,
        Guid nonConformanceId,
        CancellationToken ct)
    {
        var max = await db.CorrectiveActionEntries
            .Where(row => row.NonConformanceId == nonConformanceId)
            .MaxAsync(row => (int?)row.Version, ct);
        return max is null ? 1 : max.Value + 1;
    }
}
