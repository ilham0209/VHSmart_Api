using MediatR;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Companies;

// The dropdown lists of the Manage Companies / Company > General form (D-13, spec 7.1).
// Same platform-admin gate as the rest of the screen - spec 6.3 is Super-User only (D-07).
public record GetCompanyOptionsQuery : IRequest<CompanyOptionsResponse>;

public record CompanyOptionsResponse(
    IReadOnlyList<string> RegistrationTypes,
    IReadOnlyList<string> OwnerStatuses,
    IReadOnlyList<string> IndustrySizes,
    IReadOnlyList<string> Markets);

public class GetCompanyOptionsHandler(ICurrentUser user)
    : IRequestHandler<GetCompanyOptionsQuery, CompanyOptionsResponse>
{
    public Task<CompanyOptionsResponse> Handle(
        GetCompanyOptionsQuery request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage companies.");

        return Task.FromResult(new CompanyOptionsResponse(
            CompanyOptionsCatalog.RegistrationTypes,
            CompanyOptionsCatalog.OwnerStatuses,
            CompanyOptionsCatalog.IndustrySizes,
            CompanyOptionsCatalog.Markets));
    }
}
