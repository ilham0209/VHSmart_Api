using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.AdvancedSearch;

// The Premise filter panel of Advanced Search (spec 8, all 19 filters [CONFIRMED]): text
// filters are "contains", dropdowns are exact matches, date ranges are inclusive. One shared
// shape serves the on-screen Search and the Excel export so the file always matches the grid.
public abstract record PremiseSearchFilters
{
    public string? StoreCode { get; set; }

    public string? StoreName { get; set; }

    public Guid? BrandId { get; set; }

    public Guid? MenuConceptId { get; set; }

    public Guid? AreaManagerStaffId { get; set; }

    public Guid? OperationManagerStaffId { get; set; }

    public string? PremiseStatus { get; set; }

    public string? PremiseDocumentStatus { get; set; }

    public string? VhSmartReferenceNo { get; set; }

    public string? CbReferenceNo { get; set; }

    public Guid? CountryId { get; set; }

    public string? State { get; set; }

    public string? HalalApplicationStatus { get; set; }

    public string? HalalCertificateNo { get; set; }

    public string? HalalCertificateStatus { get; set; }

    public Guid? PremiseTagId { get; set; }

    public DateOnly? HalalExpiryFrom { get; set; }

    public DateOnly? HalalExpiryTo { get; set; }

    public DateOnly? OpeningDateFrom { get; set; }

    public DateOnly? OpeningDateTo { get; set; }

    public DateOnly? ClosingDateFrom { get; set; }

    public DateOnly? ClosingDateTo { get; set; }
}

// Query construction shared by Search and Export (same screen, one rule set):
// - premise-owned filters run in SQL; the halal-information filters are EXISTS subqueries
//   over AppBatchPremises -> AppHalalApplications -> AppHalalCertificates (Database.md 12);
// - Premise Document Status is DERIVED (D-15) and cannot be expressed as one predicate, so
//   when that filter is set the candidate ids are refined in memory first and the final
//   query pages over the refined id set (still via the DataGrid plumbing, no hand Skip/Take);
// - the certificate-status filter mirrors HalalStatusCalculator (D-04) in SQL so it can run
//   over the whole candidate set - the display columns always use the calculator itself.
internal static class PremiseSearch
{
    public static async Task<IQueryable<PremiseEntity>> BuildQueryAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        PremiseSearchFilters filters,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;
        var today = PremiseDocumentClock.Today();
        var storeCode = Lower(filters.StoreCode);
        var storeName = Lower(filters.StoreName);
        var premiseStatus = Lower(filters.PremiseStatus);
        var state = Lower(filters.State);
        var referenceNo = Lower(filters.VhSmartReferenceNo);
        var cbReferenceNo = Lower(filters.CbReferenceNo);
        var applicationStatus = Lower(filters.HalalApplicationStatus);
        var certificateNo = Lower(filters.HalalCertificateNo);

        // Date ranges are inclusive on both ends: Premise Opening/Closing are datetimes, so
        // their "to" end covers the whole day (equivalent for any time-of-day on the row);
        // certificate expiry is a DateOnly, so its "to" bound simply becomes the next day.
        var openingFrom = filters.OpeningDateFrom?.ToDateTime(TimeOnly.MinValue);
        var openingTo = filters.OpeningDateTo?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var closingFrom = filters.ClosingDateFrom?.ToDateTime(TimeOnly.MinValue);
        var closingTo = filters.ClosingDateTo?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var halalFrom = filters.HalalExpiryFrom;
        var halalTo = filters.HalalExpiryTo?.AddDays(1);

        IQueryable<PremiseEntity> query = db.Premises
            .AsNoTracking()
            .Where(premise => premise.CompanyId == companyId
                // Text filters: contains, case-insensitive (lowered constants, SQL LOWER/LIKE).
                && (storeCode == null || (premise.StoreCode ?? string.Empty).ToLower().Contains(storeCode))
                && (storeName == null || premise.Name.ToLower().Contains(storeName))
                // Dropdown filters: exact match on the id / lowered value.
                && (filters.BrandId == null || premise.BrandId == filters.BrandId)
                && (filters.MenuConceptId == null || premise.MenuConceptId == filters.MenuConceptId)
                && (filters.AreaManagerStaffId == null
                    || premise.AreaManagerStaffId == filters.AreaManagerStaffId)
                && (filters.OperationManagerStaffId == null
                    || premise.OperationManagerStaffId == filters.OperationManagerStaffId)
                && (premiseStatus == null || premise.Status.ToLower() == premiseStatus)
                && (filters.PremiseTagId == null || premise.TagId == filters.PremiseTagId)
                && (filters.CountryId == null || premise.CountryId == filters.CountryId)
                && (state == null || premise.State.ToLower() == state)
                // Date ranges: inclusive on both ends (the stored value is a datetime, so the
                // "to" end covers the whole day).
                && (openingFrom == null
                    || (premise.OpeningDate != null && premise.OpeningDate >= openingFrom))
                && (openingTo == null
                    || (premise.OpeningDate != null && premise.OpeningDate < openingTo))
                && (closingFrom == null
                    || (premise.ClosingDate != null && premise.ClosingDate >= closingFrom))
                && (closingTo == null
                    || (premise.ClosingDate != null && premise.ClosingDate < closingTo))
                // Halal-information filters: the premise matches when ANY of its applications
                // (reached only through AppBatchPremises) satisfies the condition.
                && (referenceNo == null || db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && application.ReferenceNo.ToLower().Contains(referenceNo))))
                && (cbReferenceNo == null || db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Batches.Any(batch =>
                        batch.Id == link.BatchId
                        && batch.CompanyId == companyId
                        && batch.CbReferenceNo != null
                        && batch.CbReferenceNo.ToLower().Contains(cbReferenceNo))))
                && (applicationStatus == null || db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && application.Status.ToLower() == applicationStatus)))
                && (certificateNo == null || db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && db.HalalCertificates.Any(certificate =>
                            certificate.ApplicationId == application.Id
                            && certificate.CompanyId == companyId
                            && certificate.CertificateNo.ToLower().Contains(certificateNo)))))
                && (halalFrom == null || db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && db.HalalCertificates.Any(certificate =>
                            certificate.ApplicationId == application.Id
                            && certificate.CompanyId == companyId
                            && certificate.ExpiryDate != null
                            && certificate.ExpiryDate >= halalFrom))))
                && (halalTo == null || db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && db.HalalCertificates.Any(certificate =>
                            certificate.ApplicationId == application.Id
                            && certificate.CompanyId == companyId
                            && certificate.ExpiryDate != null
                            && certificate.ExpiryDate < halalTo)))));

        var certificateStatus = filters.HalalCertificateStatus;
        if (certificateStatus is not null)
        {
            // SQL mirror of HalalStatusCalculator (D-04): Expired = expiry strictly before
            // today; Valid = null, the 9999-12-31 sentinel or a future date. The filter value
            // is fixed by the validator, so branch outside the tree - a clean translation on
            // each side. The display columns always use the calculator itself.
            if (string.Equals(
                    certificateStatus, nameof(HalalStatus.Expired), StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(premise => db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && db.HalalCertificates.Any(certificate =>
                            certificate.ApplicationId == application.Id
                            && certificate.CompanyId == companyId
                            && certificate.ExpiryDate != null
                            && certificate.ExpiryDate < today))));
            }
            else if (string.Equals(
                         certificateStatus, nameof(HalalStatus.Valid), StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(premise => db.BatchPremises.Any(link =>
                    link.PremiseId == premise.Id
                    && link.CompanyId == companyId
                    && db.Applications.Any(application =>
                        application.BatchId == link.BatchId
                        && application.CompanyId == companyId
                        && db.HalalCertificates.Any(certificate =>
                            certificate.ApplicationId == application.Id
                            && certificate.CompanyId == companyId
                            && (certificate.ExpiryDate == null
                                || certificate.ExpiryDate >= today)))));
            }
        }

        if (filters.PremiseDocumentStatus is not null)
        {
            // D-15 is a per-premise calculation over the attachment set, so the filter runs
            // after the SQL filters: refine to an id set, then page over it below.
            var candidateIds = await query.Select(premise => premise.Id).ToListAsync(ct);
            var documentStatuses = await DocumentStatusesAsync(db, candidateIds, today, ct);
            var wanted = filters.PremiseDocumentStatus;
            var matchingIds = candidateIds
                .Where(id => string.Equals(
                    documentStatuses[id], wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();
            query = db.Premises
                .AsNoTracking()
                .Where(premise => premise.CompanyId == companyId
                    && matchingIds.Contains(premise.Id));
        }

        return query;
    }

    // The screen's result row: SQL projection of the premise-owned columns plus the lookup
    // joins (the Premise List style); the two derived columns come back as placeholders and
    // are filled after paging by each caller.
    public static IQueryable<SearchPremisesResponse> Project(
        IQueryable<PremiseEntity> query,
        VHSmartDbContext db) =>
        query.Select(premise => new SearchPremisesResponse(
            premise.Id,
            premise.StoreCode ?? string.Empty,
            premise.Name,
            ((premise.Address1 ?? string.Empty) + " "
                + (premise.Address2 ?? string.Empty) + " "
                + (premise.Address3 ?? string.Empty)).Trim(),
            premise.Telephone,
            premise.Email,
            premise.GoogleMapLink,
            (from brand in db.GeneralData
             where brand.Id == premise.BrandId
             select brand.Name).FirstOrDefault() ?? string.Empty,
            (from concept in db.MenuConcepts
             where concept.Id == premise.MenuConceptId
             select concept.Name).FirstOrDefault() ?? string.Empty,
            premise.AreaManagerStaffId,
            (from staff in db.Staffs
             where staff.Id == premise.AreaManagerStaffId
             select staff.Name).FirstOrDefault() ?? string.Empty,
            premise.OperationManagerStaffId,
            (from staff in db.Staffs
             where staff.Id == premise.OperationManagerStaffId
             select staff.Name).FirstOrDefault() ?? string.Empty,
            null,
            string.Empty,
            premise.TagId,
            (from tag in db.GeneralData
             where tag.Id == premise.TagId
             select tag.Name).FirstOrDefault() ?? string.Empty));

    // The Document Status column (D-15) for a set of premises: one attachments query feeding
    // the pure calculator, the same fill the Premise List uses.
    public static async Task<IReadOnlyDictionary<Guid, string>> DocumentStatusesAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> premiseIds,
        DateOnly today,
        CancellationToken ct)
    {
        if (premiseIds.Count == 0)
            return new Dictionary<Guid, string>();

        var attachments = await db.PremiseAttachments
            .AsNoTracking()
            .Where(row => premiseIds.Contains(row.PremiseId))
            .ToListAsync(ct);

        return premiseIds.ToDictionary(
            id => id,
            id => PremiseDocumentStatusCalculator.Calculate(
                attachments
                    .Where(row => row.PremiseId == id
                        && !string.IsNullOrWhiteSpace(row.Document.StorageKey))
                    .Select(row => new PremiseDocument(
                        row.DocumentType,
                        HasUpload: true,
                        HalalStatusCalculator.ExpiryForDisplay(
                            row.ExpiryDate is null
                                ? null
                                : DateOnly.FromDateTime(row.ExpiryDate.Value))))
                    .ToList(),
                today).Text);
    }

    private static string? Lower(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

// Shared filter rules for the two requests carrying the panel (the spec gives no messages
// for these, the wording is ours): the certificate status is one of the D-04 values the
// options endpoint offers, and each date range is only checked when both ends are set.
internal static class PremiseSearchFilterRules
{
    public static void Apply<T>(AbstractValidator<T> validator)
        where T : PremiseSearchFilters
    {
        validator.RuleFor(filters => filters.HalalCertificateStatus)
            .Must(value => value is null
                || string.Equals(value, nameof(HalalStatus.Valid), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, nameof(HalalStatus.Expired), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Halal Certificate Status must be Valid or Expired.");

        validator.RuleFor(filters => filters.HalalExpiryTo)
            .Must((filters, to) => filters.HalalExpiryFrom is null
                || to is null
                || filters.HalalExpiryFrom <= to)
            .WithMessage("Halal Expiry From date must not be after the to date.");

        validator.RuleFor(filters => filters.OpeningDateTo)
            .Must((filters, to) => filters.OpeningDateFrom is null
                || to is null
                || filters.OpeningDateFrom <= to)
            .WithMessage("Opening Date from must not be after the to date.");

        validator.RuleFor(filters => filters.ClosingDateTo)
            .Must((filters, to) => filters.ClosingDateFrom is null
                || to is null
                || filters.ClosingDateFrom <= to)
            .WithMessage("Closing Date from must not be after the to date.");
    }
}
