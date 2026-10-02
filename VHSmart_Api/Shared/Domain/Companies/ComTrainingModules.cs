using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// Personnel > Internal Training (spec 7.5 "Training Module List"): Module Type comes from
// General Data TRAINING / Module Type, Module Name is required (Database.md 6) and Document
// is the optional File column group - Database.md 6 marks no asterisk on it and the spec form
// stars only Module Type* and Module Name*. Bytes live in IFileStorage (F-06).
public class TrainingModuleEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid TrainingId { get; set; }

    public TrainingEntity? Training { get; set; }

    public Guid ModuleTypeId { get; set; }

    public GeneralDataEntity? ModuleType { get; set; }

    public string ModuleName { get; set; } = string.Empty;

    public StoredFile? Document { get; set; }
}
