using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// Personnel > All Staff (spec 7.4): the staff rows a company maintains; also the source for
// Contact Person / Halal Executive (7.3), premise roles (7.7) and IHC membership (7.6).
// Required columns follow the Database.md 3 markers (* / _): Email, Title, Name, Department,
// Designation, HasTyphoidInjection, IsIhcMember. TyphoidExpiryDate is required when
// HasTyphoidInjection and IhcRoleId when IsIhcMember - both enforced in the validators, not
// by the schema (Database.md 3). UQ (CompanyId, Email) among live rows. Photo is the optional
// File column group (bytes in IFileStorage, F-06).
public class StaffEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Email { get; set; } = string.Empty;

    // Title of Honour (General Data PEOPLE, spec 7.4 "Title*").
    public Guid TitleId { get; set; }

    public GeneralDataEntity? Title { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? IdType { get; set; }

    public string? IdNumber { get; set; }

    public string? EmployeeIdNumber { get; set; }

    public string? Gender { get; set; }

    public string? Religion { get; set; }

    public Guid DepartmentId { get; set; }

    public GeneralDataEntity? Department { get; set; }

    public Guid DesignationId { get; set; }

    public GeneralDataEntity? Designation { get; set; }

    public string? OfficeNumber { get; set; }

    public string? MobileNumber { get; set; }

    public bool HasTyphoidInjection { get; set; }

    public DateTime? TyphoidExpiryDate { get; set; }

    public bool IsIhcMember { get; set; }

    public Guid? IhcRoleId { get; set; }

    public GeneralDataEntity? IhcRole { get; set; }

    public StoredFile? Photo { get; set; }
}
