namespace VHSmart_Api.Shared.Infrastructure.Sequences;

// Builds the reference numbers from D-09 through AdmDocumentSequences: the running number is
// taken in a transaction, so two callers never receive the same number for one scope.
// The formats are kept here (not in the handlers) because D-09 puts them behind this service.
public interface IReferenceNumberGenerator
{
    // {auditPrefix}/{premiseKey}/{dd-MM-yyyy}({n}), n running per prefix + date.
    Task<string> NextAuditReferenceNumberAsync(
        string auditPrefix,
        string premiseKey,
        DateOnly scheduleDate,
        CancellationToken cancellationToken = default);

    // {prefix}({schemeCode})/{ddMMyyyy}/{n}, n running per prefix + scheme + date.
    Task<string> NextApplicationReferenceNumberAsync(
        string schemeCode,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
