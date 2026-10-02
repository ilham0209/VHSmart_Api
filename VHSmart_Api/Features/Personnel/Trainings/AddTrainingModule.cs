using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Personnel.Trainings;

// "Training Module List" upload (spec 7.5: Module Type* from General Data > TRAINING > Module
// Type, Module Name*, Upload Module (file)). The training id comes from the route, the company
// from the JWT. The spec stars only Module Type and Module Name, and Database.md 6 marks
// Document as optional - so a module may be stored without bytes; the download answers 404
// until one is uploaded. File rules follow D-22's general list; bytes are stored before the
// row (CodingRules 10), exactly like the staff attachment upload.
public record AddTrainingModuleCommand(
    Guid TrainingId,
    Guid ModuleTypeId,
    string ModuleName,
    IFormFile? File) : IRequest<TrainingDetailResponse>;

public class AddTrainingModuleValidator : AbstractValidator<AddTrainingModuleCommand>
{
    public AddTrainingModuleValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.TrainingId).NotEmpty();
        RuleFor(x => x.ModuleTypeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Module type is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.TRAINING
                    && row.Category == "Module Type", ct))
            .WithMessage("Module type not found.");
        RuleFor(x => x.ModuleName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Module name is required.")
            .MaximumLength(200).WithMessage("Module name must be 200 characters or fewer.");
    }
}

public class AddTrainingModuleHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<AddTrainingModuleHandler> logger)
    : IRequestHandler<AddTrainingModuleCommand, TrainingDetailResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<TrainingDetailResponse> Handle(
        AddTrainingModuleCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var owned = await db.Trainings.AsNoTracking()
            .AnyAsync(row => row.Id == request.TrainingId && row.CompanyId == companyId, ct);
        if (!owned)
            throw new NotFoundException("Training not found.");

        StoredFile? document = null;
        if (request.File is not null)
        {
            var file = request.File;
            FileValidation.Validate(file.FileName, file.Length);

            var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
                ? mapped
                : "application/octet-stream";

            document = await storage.SaveAsync(
                file.OpenReadStream(), file.FileName, contentType, ct);
        }

        var entity = new TrainingModuleEntity
        {
            CompanyId = companyId,
            TrainingId = request.TrainingId,
            ModuleTypeId = request.ModuleTypeId,
            ModuleName = request.ModuleName,
            Document = document
        };
        db.TrainingModules.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row was rejected: drop the bytes we just wrote (if any). A storage failure
            // here must not hide the original save error, so it is only logged.
            if (document is not null)
            {
                try
                {
                    await storage.DeleteAsync(document.StorageKey, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception, "Could not remove orphaned training module {StorageKey}", document.StorageKey);
                }
            }

            throw;
        }

        return await TrainingResponseLoader.LoadAsync(db, companyId, request.TrainingId, ct);
    }
}
