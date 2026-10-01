using MediatR;
using VHSmart_Api.Features.Admin.Companies;

namespace VHSmart_Api.Features.CompanyInformation.General;

// The D-13 dropdown lists of the Company > General form (spec 7.1). Same catalog as the
// Manage Companies screen - the lists belong to the form, not to the screen that edits it -
// but this query is gated by Company.General so a company user can fill the form without the
// platform-admin permission the Admin options endpoint requires.
public record GetCompanyGeneralOptionsQuery : IRequest<CompanyOptionsResponse>;

public class GetCompanyGeneralOptionsHandler
    : IRequestHandler<GetCompanyGeneralOptionsQuery, CompanyOptionsResponse>
{
    public Task<CompanyOptionsResponse> Handle(
        GetCompanyGeneralOptionsQuery request,
        CancellationToken ct) =>
        Task.FromResult(new CompanyOptionsResponse(
            CompanyOptionsCatalog.RegistrationTypes,
            CompanyOptionsCatalog.OwnerStatuses,
            CompanyOptionsCatalog.IndustrySizes,
            CompanyOptionsCatalog.Markets));
}
