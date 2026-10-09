using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// Shared mapping and loaders for the Manage Batch screens (spec 12.2). One place where a
// command becomes a row so Create and Update can never disagree about the mapping (same
// stance as ProductData), plus the two derived-data helpers the pickers and the detail use.
internal static class BatchData
{
    public static BatchEntity Apply(
        BatchEntity entity,
        Guid schemeId,
        string name,
        string? cbReferenceNo,
        DateTime? submissionPlannedDate,
        Guid? brandId,
        Guid? manufacturerSupplierId,
        string? description)
    {
        entity.SchemeId = schemeId;
        entity.Name = name;
        entity.CbReferenceNo = cbReferenceNo;
        entity.SubmissionPlannedDate = submissionPlannedDate;
        entity.BrandId = brandId;
        entity.ManufacturerSupplierId = manufacturerSupplierId;
        entity.Description = description;
        return entity;
    }

    // The scheme drives the batch behaviour (spec 12.2, Database.md 10): a Food Premise
    // scheme links premises + a brand, every other scheme links products + a manufacturer.
    public static async Task<bool> IsFoodPremiseSchemeAsync(
        VHSmartDbContext db,
        Guid schemeId,
        CancellationToken ct) =>
        await db.Schemes.AsNoTracking()
            .AnyAsync(row => row.Id == schemeId && row.IsFoodPremise, ct);

    // D-15: only premises whose five required document types are uploaded and none expired
    // ("COMPLETE DOCUMENTATION") may join a batch - the Batch > Associate Premise pick-list
    // rule. The calculator is pure, so the attachments are loaded once and the caller passes
    // the ids it cares about (the picker passes the caller's own premises, the link guard
    // one). LoadPremiseStatusesAsync is the same computation returning the status text the
    // application screen's Establishment tab shows (HA-03).
    public static async Task<Dictionary<Guid, PremiseDocumentStatus>> LoadPremiseStatusesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> premiseIds,
        CancellationToken ct)
    {
        var statuses = new Dictionary<Guid, PremiseDocumentStatus>();
        if (premiseIds.Count == 0)
            return statuses;

        var attachments = await db.PremiseAttachments.AsNoTracking()
            .Where(row => premiseIds.Contains(row.PremiseId))
            .ToListAsync(ct);
        var documentsByPremise = attachments
            .Where(row => !string.IsNullOrWhiteSpace(row.Document.StorageKey))
            .GroupBy(row => row.PremiseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<PremiseDocument>)group
                    .Select(row => new PremiseDocument(
                        row.DocumentType,
                        HasUpload: true,
                        HalalStatusCalculator.ExpiryForDisplay(
                            row.ExpiryDate is null
                                ? null
                                : DateOnly.FromDateTime(row.ExpiryDate.Value))))
                    .ToList());

        var today = PremiseDocumentClock.Today();
        foreach (var premiseId in premiseIds.Distinct())
        {
            statuses[premiseId] = PremiseDocumentStatusCalculator.Calculate(
                documentsByPremise.TryGetValue(premiseId, out var documents) ? documents : [],
                today);
        }

        return statuses;
    }

    public static async Task<HashSet<Guid>> LoadCompletePremiseIdsAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> premiseIds,
        CancellationToken ct)
    {
        var statuses = await LoadPremiseStatusesAsync(db, premiseIds, ct);
        return [.. statuses
            .Where(pair => pair.Value.IsComplete)
            .Select(pair => pair.Key)];
    }
}

// One row of the edit modal's "List of Products" as the link endpoint answers it: product
// name + brand joined here rather than through a navigation so the list, the detail and a
// link can never disagree about the row (same reasoning as ProductResponseData). The Link
// Ingredient Status word comes from the product list's own constant, and the status itself
// is derived by the same D-18 code path the product screens use.
internal static class BatchProductRowData
{
    public static async Task<BatchProductResponse> LoadAsync(
        VHSmartDbContext db,
        BatchProductEntity link,
        CancellationToken ct)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(row => row.Id == link.ProductId)
            .Select(row => new
            {
                row.Name,
                BrandName = db.GeneralData
                    .Where(data => data.Id == row.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        var halalInfo = await ProductHalalInformation.LoadManyAsync(
            db, [link.ProductId], ct);
        var halal = halalInfo.GetValueOrDefault(link.ProductId);

        return new BatchProductResponse(
            link.Id,
            link.ProductId,
            product?.Name ?? string.Empty,
            product?.BrandName ?? string.Empty,
            halal?.IsIngredientLinked == true
                ? ProductIngredientLinkStatus.Linked
                : ProductIngredientLinkStatus.Unlinked,
            link.MappingStatus);
    }
}
