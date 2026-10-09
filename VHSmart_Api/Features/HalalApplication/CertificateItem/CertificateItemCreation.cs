using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.HalalApplication.CertificateItem;

// Database.md 10: AppCertificateItems are "created when the app becomes approved". The only
// path that reaches APPLICATION APPROVED is "Tagging Application Status", so the tagging
// handler calls this helper the first time the status crosses the approval threshold -
// including a skip straight past it, because D-26 lets the dialog jump to any later status.
// Each ACTIVE batch product snapshots as a product item (its own name and brand) and each
// linked batch premise as a premise item (its own brand, falling back to the batch's - the
// brand the application was filed under). Runs are idempotent: an application that already
// has rows (re-tag, repeated approval) is left alone.
internal static class CertificateItemCreation
{
    public static async Task CreateOnApprovalAsync(
        VHSmartDbContext db,
        ApplicationEntity application,
        CancellationToken ct)
    {
        if (application.BatchId is not { } batchId)
            return;

        if (await db.CertificateItems.AnyAsync(
                row => row.ApplicationId == application.Id, ct))
            return;

        var batch = await db.Batches.FirstOrDefaultAsync(
            row => row.Id == batchId && row.CompanyId == application.CompanyId, ct);
        if (batch is null)
            return;

        var products = await db.BatchProducts
            .Where(row => row.BatchId == batchId
                && row.MappingStatus == BatchProductMappingStatus.Active)
            .Select(row => new
            {
                row.ProductId,
                Product = db.Products
                    .Where(product => product.Id == row.ProductId)
                    .Select(product => new { product.Name, product.BrandId })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var link in products)
        {
            if (link.Product is null)
                continue;

            db.CertificateItems.Add(new CertificateItemEntity
            {
                CompanyId = application.CompanyId,
                ApplicationId = application.Id,
                ItemName = link.Product.Name ?? string.Empty,
                ProductId = link.ProductId,
                BrandId = link.Product.BrandId
            });
        }

        var premises = await db.BatchPremises
            .Where(row => row.BatchId == batchId)
            .Select(row => new
            {
                row.PremiseId,
                Premise = db.Premises
                    .Where(premise => premise.Id == row.PremiseId)
                    .Select(premise => new { premise.Name, premise.BrandId })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var link in premises)
        {
            if (link.Premise is null)
                continue;

            db.CertificateItems.Add(new CertificateItemEntity
            {
                CompanyId = application.CompanyId,
                ApplicationId = application.Id,
                ItemName = link.Premise.Name,
                PremiseId = link.PremiseId,
                BrandId = link.Premise.BrandId ?? batch.BrandId
            });
        }
    }
}
