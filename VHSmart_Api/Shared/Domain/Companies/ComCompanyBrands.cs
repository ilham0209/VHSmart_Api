using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// The brand links of Manage Companies (spec 6.3, D-20 - Database.md 3): the brands themselves
// are AdmGeneralData (Group COMPANY, Category Brand) rows maintained through General Data,
// this table only says which company uses which brand and the platform admin sets it. UQ
// (CompanyId, BrandId); unlinking is a soft delete (CodingRules 7.1) so the filtered unique
// index frees the pair for a later re-link.
public class CompanyBrandEntity : BaseClass
{
    public Guid CompanyId { get; set; }

    public Guid BrandId { get; set; }

    public GeneralDataEntity? Brand { get; set; }
}
