using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Submit Application (spec 12.5 tab, 12.6 prerequisites): the acknowledgement travels with
// the submit (the form has its own Save - the button posts the whole block), the D-26
// prerequisites are checked server side (a batch with products or premises, CB Application
// No. and Date, D-18-valid raw materials - Q10 blocks) and the application moves
// Draft -> "PROCESSING AT {CB} (NEW)" with its status-history row (D-26). "Application tabs
// completed" (§12.6) is NOT enforced: the spec never documented the per-field rules
// [VERIFY - flagged]. Identity comes from the JWT.
public record SubmitApplicationCommand(
    Guid Id,
    string AckName,
    string AckEmail,
    string AckMobile,
    bool AckAccepted) : IRequest<ApplicationSubmitResponse>;

// Submit response: what the screen's header refreshes after the post (the acknowledgement
// block itself is shown read-only from these columns).
public record ApplicationSubmitResponse(
    Guid Id,
    string ReferenceNo,
    string Status,
    DateTime StatusDate,
    DateTime? SubmittedAt);

public class SubmitApplicationValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationValidator()
    {
        RuleFor(x => x.AckName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Acknowledgement name is required.")
            .MaximumLength(200)
            .WithMessage("Acknowledgement name must be 200 characters or fewer.");

        RuleFor(x => x.AckEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Acknowledgement email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(254)
            .WithMessage("Acknowledgement email must be 254 characters or fewer.");

        RuleFor(x => x.AckMobile)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Acknowledgement mobile number is required.")
            .MaximumLength(30)
            .WithMessage("Acknowledgement mobile number must be 30 characters or fewer.");

        // The checkbox of spec 12.5 is a submit prerequisite (§12.6). The message is ours.
        RuleFor(x => x.AckAccepted)
            .Equal(true).WithMessage("The acknowledgement must be accepted.");
    }
}

public class SubmitApplicationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<SubmitApplicationCommand, ApplicationSubmitResponse>
{
    public async Task<ApplicationSubmitResponse> Handle(
        SubmitApplicationCommand request,
        CancellationToken ct)
    {
        var application = await db.Applications
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Application not found.");

        // D-26: only DRAFT may be submitted; everything else already took the read-only rule.
        ApplicationData.EnsureDraft(application);

        // §12.6: a batch/product (or premises) selected. The batch was validated as one of
        // the caller's own when it was chosen (HA-03); a soft-deleted or replaced row no
        // longer counts as selected.
        var batchId = application.BatchId;
        if (batchId is null
            || !await db.Batches.AsNoTracking().AnyAsync(
                row => row.Id == batchId && row.CompanyId == application.CompanyId, ct))
        {
            throw new BusinessRuleException("A batch must be selected before submission.");
        }

        // "Batch/product (or premises) selected": a product batch carries ACTIVE links, a
        // Food Premise batch premises - one of the two must be there.
        var productIds = await db.BatchProducts.AsNoTracking()
            .Where(row => row.BatchId == batchId
                && row.CompanyId == application.CompanyId
                && row.MappingStatus == BatchProductMappingStatus.Active)
            .Select(row => row.ProductId)
            .ToListAsync(ct);
        var hasPremises = await db.BatchPremises.AsNoTracking()
            .AnyAsync(row => row.BatchId == batchId && row.CompanyId == application.CompanyId,
                ct);
        if (productIds.Count == 0 && !hasPremises)
            throw new BusinessRuleException("The selected batch has no products or premises.");

        // Owner rule "raw materials must be halal" (§12.6) + D-18 with Q10 = Block: every
        // batch product must still be Valid at submit time - a certificate may have expired
        // since LinkBatchProduct let the product in, so the check runs again here through
        // the product screen's own derivation. Food Premise batches skip it (no products).
        if (productIds.Count > 0)
        {
            var halalInfo = await ProductHalalInformation.LoadManyAsync(db, productIds, ct);
            if (productIds.Any(productId => halalInfo.GetValueOrDefault(productId)
                    is not { IsIngredientLinked: true, HalalStatus: HalalStatus.Valid }))
            {
                throw new BusinessRuleException(
                    "Batch products must have valid halal raw materials before submission.");
            }
        }

        // §12.6: CB Application No. and CB Application Date are mandatory on submit.
        if (string.IsNullOrWhiteSpace(application.CbApplicationNo))
            throw new BusinessRuleException("CB application number is required.");
        if (application.CbApplicationDate is null)
            throw new BusinessRuleException("CB application date is required.");

        // D-26 / spec 12.7 [MANUAL]: Draft -> "PROCESSING AT {CB} (NEW)", the CB name of the
        // application's own company (the CB name varies by company - unknown which field, so
        // the Name is used; ours beyond the D-26 template).
        var certificationBodyId = await db.Companies.AsNoTracking()
            .Where(row => row.Id == application.CompanyId)
            .Select(row => row.CertificationBodyId)
            .FirstOrDefaultAsync(ct);
        var certificationBodyName = await db.CertificationBodies.AsNoTracking()
            .Where(row => row.Id == certificationBodyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct);
        var status = ApplicationData.ProcessingStatus(certificationBodyName);

        var now = DateTime.UtcNow;
        var changedBy = Guid.TryParse(user.UserId, out var parsed) ? parsed : (Guid?)null;
        application.Status = status;
        application.StatusDate = now;
        application.AckName = request.AckName;
        application.AckEmail = request.AckEmail;
        application.AckMobile = request.AckMobile;
        application.AckAccepted = request.AckAccepted;
        application.SubmittedAt = now;
        application.SubmittedBy = changedBy;

        // D-26: every status change writes AppApplicationStatusHistories.
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistoryEntity
        {
            CompanyId = application.CompanyId,
            ApplicationId = application.Id,
            FromStatus = ApplicationStatus.Draft,
            ToStatus = status,
            ChangedAt = now,
            ChangedBy = changedBy
        });
        await db.SaveChangesAsync(ct);

        return new ApplicationSubmitResponse(
            application.Id,
            application.ReferenceNo,
            application.Status,
            application.StatusDate,
            application.SubmittedAt);
    }
}
