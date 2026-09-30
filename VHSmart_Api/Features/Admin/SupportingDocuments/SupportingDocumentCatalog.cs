using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// Spec 5.5: "+ template upload for SOP views" - the three SOP views are the ones whose rows may
// carry a template file; TRAINING / RAW MATERIAL / HALAL APPLICATION / ALL STAFF are pure
// dropdown entries for their modules. Used by the create/update validators (a template on a
// non-SOP view is rejected) and by update, which drops the template when the row leaves the
// SOP views.
public static class SupportingDocumentCatalog
{
    public static bool IsSopView(SupportingDocumentForView forView) =>
        forView is SupportingDocumentForView.SopDocumentsAndRecords
            or SupportingDocumentForView.SopHas
            or SupportingDocumentForView.SopIhcs;
}
