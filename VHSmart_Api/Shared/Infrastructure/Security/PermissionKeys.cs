namespace VHSmart_Api.Shared.Infrastructure.Security;

// Screen keys from CodingRules Appendix A - one per screen, each with the 4 PermissionAction
// values. The constant's value is the Appendix A key verbatim (that is what AdmRolePermissions
// stores and what the policy name carries); the constant's name is the same key with the dots
// removed so it is a valid identifier. Do not copy the legacy GC/MD codes.
public static class PermissionKeys
{
    public const string Dashboard = "Dashboard";

    public const string AdminGeneralData = "Admin.GeneralData";

    public const string AdminCertificationBodies = "Admin.CertificationBodies";

    public const string AdminServiceProviders = "Admin.ServiceProviders";

    public const string AdminWebLinks = "Admin.WebLinks";

    public const string AdminSupportingDocuments = "Admin.SupportingDocuments";

    public const string AdminCompanies = "Admin.Companies";

    public const string AdminUsers = "Admin.Users";

    public const string AdvancedSearch = "AdvancedSearch";

    public const string CompanyGeneral = "Company.General";

    public const string CompanyHalalPolicy = "Company.HalalPolicy";

    public const string CompanyProfiles = "Company.Profiles";

    public const string CompanyInternalHalalCommittee = "Company.InternalHalalCommittee";

    public const string PersonnelAllStaff = "Personnel.AllStaff";

    public const string PersonnelInternalTraining = "Personnel.InternalTraining";

    public const string PremiseManagePremise = "Premise.ManagePremise";

    public const string ProductManageProduct = "Product.ManageProduct";

    public const string ProductManageMenu = "Product.ManageMenu";

    public const string ProductManageMenuConcept = "Product.ManageMenuConcept";

    public const string ProductVerifyHalalProductUpdate = "Product.VerifyHalalProductUpdate";

    public const string RawMaterialManufacturerSupplier = "RawMaterial.ManufacturerSupplier";

    public const string RawMaterialMasterList = "RawMaterial.MasterList";

    public const string HalalApplicationManageBatch = "HalalApplication.ManageBatch";

    public const string HalalApplicationMyApplication = "HalalApplication.MyApplication";

    public const string HalalApplicationCertificateItem = "HalalApplication.CertificateItem";

    public const string HalalApplicationHalalCertificate = "HalalApplication.HalalCertificate";

    public const string AuditAuditPrefix = "Audit.AuditPrefix";

    public const string AuditRecommendation = "Audit.Recommendation";

    public const string AuditFinding = "Audit.Finding";

    public const string AuditAuditCriteria = "Audit.AuditCriteria";

    public const string AuditAuditChecklist = "Audit.AuditChecklist";

    public const string AuditGroupAuditor = "Audit.GroupAuditor";

    public const string AuditAuditPlanning = "Audit.AuditPlanning";

    public const string AuditAuditTask = "Audit.AuditTask";

    public const string AuditExternalReport = "Audit.ExternalReport";

    public const string AuditNonConformance = "Audit.NonConformance";

    public const string PaymentCertificate = "Payment.Certificate";

    public const string PaymentOther = "Payment.Other";

    public const string ReferenceView = "Reference.View";

    public const string SupportSubmit = "Support.Submit";

    public const string AccountSetting = "Account.Setting";
}
